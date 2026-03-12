using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Drive.v3.Data;


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
    }
}
