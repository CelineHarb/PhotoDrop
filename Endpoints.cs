using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using PhotoDrop.Services;
using PhotoDrop.Models;

namespace PhotoDrop;

public static class Endpoints
{
    public static void MapPhotoDropEndpoints(this WebApplication app)
    {
        // ===== Auth =====

        app.MapGet("/auth/google/start", (HttpContext ctx, string eventName) =>
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = "/auth/google/finish"
            };
            props.Items["eventName"] = eventName;
            return Results.Challenge(props, new[] { GoogleDefaults.AuthenticationScheme });
        });

        app.MapGet("/auth/google/finish", async (HttpContext ctx, GoogleDriveService driveSvc, EventStorage eventStorage) =>
        {
            var accessToken = await ctx.GetTokenAsync("access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
                return Results.Redirect("/get-started?error=missing_access_token");

            var refreshToken = await ctx.GetTokenAsync("refresh_token");
            var expiresAt = await ctx.GetTokenAsync("expires_at");

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

            createdEvent.RefreshToken = refreshToken;
            if (DateTime.TryParse(expiresAt, out var parsedExpiry))
                createdEvent.TokenExpiresAt = parsedExpiry;
            eventStorage.Update(createdEvent);

            return Results.Redirect($"/get-started?connected=1&token={createdEvent.GuestToken}");
        });

        // ===== Storage =====

        app.MapGet("/api/storage/{token}", async (string token, EventStorage eventStorage, GoogleDriveService driveSvc, TokenService tokenService) =>
        {
            var eventRecord = eventStorage.GetByToken(token);
            if (eventRecord is null)
                return Results.NotFound(new { error = "Event not found." });

            var accessToken = await tokenService.GetValidAccessTokenAsync(eventRecord);
            if (accessToken is null)
                return Results.BadRequest(new { error = "Unable to access Google Drive." });

            var storage = await driveSvc.GetStorageInfoAsync(accessToken);

            return Results.Ok(new
            {
                availableFormatted = storage.AvailableFormatted,
                totalFormatted = storage.TotalFormatted,
                usedFormatted = storage.UsedFormatted,
                estimatedPhotos = storage.EstimatedPhotoCapacity,
                availableBytes = storage.AvailableBytes
            });
        });

        // ===== QR Code =====

        app.MapGet("/api/qr/{token}", (string token, EventStorage eventStorage, QrCodeService qrService, HttpRequest request) =>
        {
            var eventRecord = eventStorage.GetByToken(token);
            if (eventRecord is null)
                return Results.NotFound("Event not found.");

            var guestUrl = $"{request.Scheme}://{request.Host}/e/{token}";
            var pngBytes = qrService.GeneratePng(guestUrl);
            return Results.File(pngBytes, "image/png", $"photodrop-{eventRecord.EventName}-qr.png");
        });

        // ===== Presigned Upload URL =====

        app.MapPost("/api/upload-url/{token}", async (string token, HttpRequest request, EventStorage eventStorage, CloudStorageService cloudStorage, GuestSessionService sessionService) =>
        {
            var eventRecord = eventStorage.GetByToken(token);
            if (eventRecord is null)
                return Results.NotFound(new { error = "Event not found." });

            if (eventRecord.PhotoLimit.HasValue && eventRecord.PhotoCount >= eventRecord.PhotoLimit.Value)
                return Results.BadRequest(new { error = "This event has reached its photo limit." });

            var body = await request.ReadFromJsonAsync<UploadUrlRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.FileName) || string.IsNullOrWhiteSpace(body.ContentType))
                return Results.BadRequest(new { error = "fileName and contentType are required." });

            var sessionToken = body.SessionToken;
            if (string.IsNullOrWhiteSpace(sessionToken))
                return Results.BadRequest(new { error = "No session found." });

            var ipAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString();
            var session = sessionService.GetOrCreateSession(eventRecord.Id, sessionToken, ipAddress);

            var guestLimit = eventRecord.PhotoLimit ?? 20;
            if (session.PhotosUploaded >= guestLimit)
                return Results.BadRequest(new { error = $"You've reached your limit of {guestLimit} photos." });

            if (!FileValidation.AllowedContentTypes.Contains(body.ContentType))
                return Results.BadRequest(new { error = "Invalid file type." });

            var ext = Path.GetExtension(body.FileName);
            if (!FileValidation.AllowedExtensions.Contains(ext))
                return Results.BadRequest(new { error = "Invalid file extension." });

            var safeName = System.Text.RegularExpressions.Regex.Replace(body.FileName, @"[^a-zA-Z0-9._-]", "-");
            var objectName = $"{eventRecord.Id}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{safeName}";

            var url = cloudStorage.GenerateUploadUrl(objectName, body.ContentType);

            return Results.Ok(new { uploadUrl = url, objectName = objectName });
        });

        // ===== Transfer from Cloud Storage to Google Drive (parallel) =====

        app.MapPost("/api/transfer/{token}", async (string token, HttpRequest request, EventStorage eventStorage, CloudStorageService cloudStorage,
            GoogleDriveService driveSvc, TokenService tokenService, GuestSessionService sessionService) =>
        {
            var eventRecord = eventStorage.GetByToken(token);
            if (eventRecord is null)
                return Results.NotFound(new { error = "Event not found." });

            var accessToken = await tokenService.GetValidAccessTokenAsync(eventRecord);
            if (accessToken is null)
                return Results.BadRequest(new { error = "Unable to access Google Drive. Host may need to reconnect." });

            var transferBody = await request.ReadFromJsonAsync<TransferRequest>();
            var sessionToken = transferBody?.SessionToken;

            var prefix = $"{eventRecord.Id}/";
            var files = await cloudStorage.ListFilesAsync(prefix);

            if (files.Count == 0)
                return Results.Ok(new { transferred = 0 });

            // Transfers all files in parallel for speed.
            // Each file downloads from Cloud Storage and uploads to Drive simultaneously
            // instead of one at a time. Task.WhenAll runs all transfers at once.
            var tasks = files.Select(async fileName =>
            {
                try
                {
                    using var stream = await cloudStorage.DownloadFileAsync(fileName);
                    var simpleName = Path.GetFileName(fileName);
                    var contentType = simpleName.EndsWith(".png") ? "image/png" : "image/jpeg";

                    await driveSvc.UploadPhotoAsync(accessToken, eventRecord.FolderId, stream, simpleName, contentType);
                    await cloudStorage.DeleteFileAsync(fileName);
                    return true;
                }
                catch
                {
                    return false;
                }
            });

            var results = await Task.WhenAll(tasks);
            var transferred = results.Count(r => r);

            eventRecord.PhotoCount += transferred;
            eventStorage.Update(eventRecord);

            if (!string.IsNullOrWhiteSpace(sessionToken) && transferred > 0)
            {
                var session = sessionService.GetOrCreateSession(eventRecord.Id, sessionToken, null);
                sessionService.IncrementPhotoCount(session.Id, transferred);
            }

            return Results.Ok(new { transferred });
        });

        // ===== Guest Upload Count =====

        app.MapGet("/api/guest-uploads/{token}", (string token, string session, EventStorage eventStorage, GuestSessionService sessionService) =>
        {
            var eventRecord = eventStorage.GetByToken(token);
            if (eventRecord is null)
                return Results.NotFound(new { error = "Event not found." });

            var count = sessionService.GetPhotoCount(eventRecord.Id, session);
            return Results.Ok(new { photosUploaded = count });
        });
    }
}