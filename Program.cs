using PhotoDrop.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<PhotoDrop.Services.GoogleDriveService>();

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

app.MapGet("/auth/google/finish", async (HttpContext ctx, PhotoDrop.Services.GoogleDriveService driveSvc) =>
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

    // For now, showing it in query string so we can see it worked
    return Results.Redirect($"/get-started?connected=1&folderId={folderId}");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
