using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using DriveFile = Google.Apis.Drive.v3.Data.File;


namespace PhotoDrop.Services
{
    public class GoogleDriveService
    {
        public async Task<string> CreateEventFolderAsync(string accessToken, string eventName)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var drive = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "PhotoDrop"
            });

            var folder = new Google.Apis.Drive.v3.Data.File
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
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var drive = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "PhotoDrop"
            });

            var fileMetadata = new DriveFile
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };

            var request = drive.Files.Create(fileMetadata, fileStream, contentType);
            request.Fields = "id";

            await request.UploadAsync();

            if (request.ResponseBody == null || string.IsNullOrWhiteSpace(request.ResponseBody.Id))
            {
                throw new Exception("Google Drive upload failed.");
            }

            return request.ResponseBody.Id;
        }
    }
}
