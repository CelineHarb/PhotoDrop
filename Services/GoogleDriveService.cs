using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using PhotoDrop.Models;

namespace PhotoDrop.Services;

public class GoogleDriveService
{
    // Creates a DriveService from an access token — used by all methods
    private DriveService CreateDriveClient(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PhotoDrop"
        });
    }

    public async Task<string> CreateEventFolderAsync(string accessToken, string eventName)
    {
        var drive = CreateDriveClient(accessToken);

        var folder = new DriveFile
        {
            Name = $"PhotoDrop - {eventName}",
            MimeType = "application/vnd.google-apps.folder"
        };

        var request = drive.Files.Create(folder);
        request.Fields = "id";
        var created = await request.ExecuteAsync();
        return created.Id;
    }

    public async Task<string> UploadPhotoAsync(string accessToken, string folderId, Stream fileStream, string fileName, string contentType)
    {
        var drive = CreateDriveClient(accessToken);

        var fileMetadata = new DriveFile
        {
            Name = fileName,
            Parents = new List<string> { folderId }
        };

        var request = drive.Files.Create(fileMetadata, fileStream, contentType);
        request.Fields = "id";
        await request.UploadAsync();

        if (request.ResponseBody == null || string.IsNullOrWhiteSpace(request.ResponseBody.Id))
            throw new Exception("Google Drive upload failed.");

        return request.ResponseBody.Id;
    }

    // Checks how much storage the host has available on their Google Drive
    public async Task<DriveStorageInfo> GetStorageInfoAsync(string accessToken)
    {
        var drive = CreateDriveClient(accessToken);

        var about = drive.About.Get();
        about.Fields = "storageQuota";
        var result = await about.ExecuteAsync();

        var quota = result.StorageQuota;

        // Google returns these as nullable longs in bytes
        long totalBytes = quota.Limit ?? 0;
        long usedBytes = quota.Usage ?? 0;
        long availableBytes = totalBytes - usedBytes;
        if (availableBytes < 0) availableBytes = 0;

        return new DriveStorageInfo
        {
            TotalBytes = totalBytes,
            UsedBytes = usedBytes,
            AvailableBytes = availableBytes
        };
    }
}