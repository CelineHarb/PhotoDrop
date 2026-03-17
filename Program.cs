using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json.Linq;
using PhotoDrop.Components;
using PhotoDrop.Services;
using PhotoDrop.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});

builder.Services.AddSingleton<GoogleDriveService>();
builder.Services.AddSingleton<EventStorage>();
builder.Services.AddSingleton<QrCodeService>();
builder.Services.AddSingleton<CloudStorageService>();

//Authentication + Google OAuth (store who is signed in)
builder.Services.AddAuthentication( options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    //wires app to Google OAuth using our credentials in user secrets
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;

    //Permission we're requesting from Google
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file"); // allows app to create/manage files it creates in the user's Drive

    //after OAuth finishes, store access_token, refresh_token, expires_at, somewhere I can retrieve later 
    options.SaveTokens = true;

    // Helps ensure you get a refresh token for later uploads when host is not online
    options.AccessType = "offline";
});

builder.Services.AddAuthorization();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// auth middleware 
app.UseAuthentication(); // read the cookie on every request 
app.UseAuthorization(); //enforce access rules

// endpoints 
app.MapGet("/auth/google/start", (HttpContext ctx, string eventName) =>
{
    var props = new AuthenticationProperties
    {
        RedirectUri = "/auth/google/finish"
    };

    props.Items["eventName"] = eventName; // temporary, for later use
    return Results.Challenge(props, new[] { GoogleDefaults.AuthenticationScheme }); // starts the google oauth flow
});

app.MapGet("/auth/google/finish", async (HttpContext ctx, GoogleDriveService driveSvc, EventStorage eventStorage) =>
{
    var accessToken = await ctx.GetTokenAsync("access_token");
    if (string.IsNullOrWhiteSpace(accessToken))
        return Results.Redirect("/get-started?error=missing_access_token");

    //pull eventName we stored in endpoint
    var authResult = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    string? eventName = null;

    if (authResult.Properties?.Items != null &&
        authResult.Properties.Items.ContainsKey("eventName"))
    {
        eventName = authResult.Properties.Items["eventName"];
    }

    if (string.IsNullOrWhiteSpace(eventName) || eventName.Trim().Length < 3)
        return Results.Redirect("/get-started?error=missing_event_name");

    var cleanedEventName = eventName.Trim();

    var folderId = await driveSvc.CreateEventFolderAsync(accessToken, cleanedEventName);

    var createdEvent = eventStorage.Add(cleanedEventName, folderId, accessToken);

    // For now, showing it in query string so we can see it worked
    return Results.Redirect($"/get-started?connected=1&token={createdEvent.GuestToken}");
});

app.MapPost("/api/upload/{token}", async (
    string token,
    HttpRequest request,EventStorage eventStorage, GoogleDriveService driveSvc) =>
{
    // Validate even token
    var eventRecord = eventStorage.GetByToken(token);
    if (eventRecord == null)
        return Results.BadRequest("Invalid event token.");

    // Check content type 
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected multipart form data.");

    // Check total request size 
    if (request.ContentLength > 50 * 1024 * 1024)
        return Results.BadRequest(new { error = "Total upload size exceeds 50 MB." });

    // Read form
    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync();
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid form data." });
    }

    var files = form.Files;

    // Validate file count
    const int maxFilesPerRequest = 20;
    if (files.Count == 0)
        return Results.BadRequest(new { error = "No files provided." });
    if (files.Count > maxFilesPerRequest)
        return Results.BadRequest(new { error = $"Maximum {maxFilesPerRequest} files per upload." });

    // Allowed types
    var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/heic", "image/heif", "image/webp"
    };
    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp"
    };
    const long maxFileSize = 15 * 1024 * 1024; // 15 MB per file

    var results = new List<object>();

    foreach (var file in files)
    {
        var ext = Path.GetExtension(file.FileName);

        // Validate content type AND extension
        if (!allowedTypes.Contains(file.ContentType) || !allowedExtensions.Contains(ext))
        {
            results.Add(new { file = file.FileName, status = "rejected", reason = "Invalid file type. Only JPG, PNG, HEIC, and WebP are allowed." });
            continue;
        }

        if (file.Length > maxFileSize)
        {
            results.Add(new { file = file.FileName, status = "rejected", reason = "File exceeds 15 MB." });
            continue;
        }

        if (file.Length == 0)
        {
            results.Add(new { file = file.FileName, status = "rejected", reason = "Empty file." });
            continue;
        }

        // Validate magic bytes (file header)
        using var stream = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await stream.ReadAsync(header, 0, 12);
        stream.Position = 0; // Reset for upload

        if (!FileValidation.IsValidImageHeader(header, bytesRead))
        {
            results.Add(new { file = file.FileName, status = "rejected", reason = "File content does not match an image format." });
            continue;
        }

        try
        {
            await driveSvc.UploadPhotoAsync(
                eventRecord.AccessToken,
                eventRecord.FolderId,
                stream,
                file.FileName,
                file.ContentType
            );
            results.Add(new { file = file.FileName, status = "uploaded" });
        }
        catch
        {
            results.Add(new { file = file.FileName, status = "failed", reason = "Upload to Drive failed." });
        }
    }

    var uploadedCount = results.Count(r => ((dynamic)r).status == "uploaded");
    return Results.Ok(new { uploaded = uploadedCount, details = results });
});

app.MapGet("/api/qr/{token}", (string token, EventStorage eventStorage, QrCodeService qrService, HttpRequest request) =>
{
    var eventRecord = eventStorage.GetByToken(token);
    if (eventRecord is null)
        return Results.NotFound("Event not found.");

    var guestUrl = $"{request.Scheme}://{request.Host}/e/{token}";
    var pngBytes = qrService.GeneratePng(guestUrl);
    return Results.File(pngBytes, "image/png", $"photodrop-{eventRecord.EventName}-qr.png");
});

//Guest requests a presigned upload URL for each file
app.MapPost("/api/upload-url/{token}", async (string token, HttpRequest request, EventStorage eventStorage, CloudStorageService cloudStorage) =>
{
    var eventRecord = eventStorage.GetByToken(token);
    if (eventRecord is null)
        return Results.NotFound(new { error = "Event not found." });

    // Read the file info from the request body
    var body = await request.ReadFromJsonAsync<UploadUrlRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.FileName) || string.IsNullOrWhiteSpace(body.ContentType))
        return Results.BadRequest(new { error = "fileName and contentType are required." });

    // Validate file type
    if (!FileValidation.AllowedContentTypes.Contains(body.ContentType))
        return Results.BadRequest(new { error = "Invalid file type." });

    var ext = Path.GetExtension(body.FileName);
    if (!FileValidation.AllowedExtensions.Contains(ext))
        return Results.BadRequest(new { error = "Invalid file extension." });

    // Build a unique object name: eventId/timestamp-filename
    var objectName = $"{eventRecord.Id}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{body.FileName}";

    var url = cloudStorage.GenerateUploadUrl(objectName, body.ContentType);

    return Results.Ok(new { uploadUrl = url, objectName = objectName });
});

// After guest uploads finish, move files from Cloud Storage to the host's Google Drive
app.MapPost("/api/transfer/{token}", async (
    string token,
    EventStorage eventStorage,
    CloudStorageService cloudStorage,
    GoogleDriveService driveSvc) =>
{
    var eventRecord = eventStorage.GetByToken(token);
    if (eventRecord is null)
        return Results.NotFound(new { error = "Event not found." });

    var prefix = $"{eventRecord.Id}/";
    var files = await cloudStorage.ListFilesAsync(prefix);

    if (files.Count == 0)
        return Results.Ok(new { transferred = 0 });

    int transferred = 0;

    foreach (var fileName in files)
    {
        try
        {
            using var stream = await cloudStorage.DownloadFileAsync(fileName);
            var simpleName = Path.GetFileName(fileName);
            var contentType = simpleName.EndsWith(".png") ? "image/png" : "image/jpeg";

            await driveSvc.UploadPhotoAsync(
                eventRecord.AccessToken,
                eventRecord.FolderId,
                stream,
                simpleName,
                contentType
            );

            await cloudStorage.DeleteFileAsync(fileName);
            transferred++;
        }
        catch
        {
            // Skip failed files, continue with the rest
        }
    }

    return Results.Ok(new { transferred });
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
