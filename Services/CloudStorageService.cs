using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;

namespace PhotoDrop.Services;

public class CloudStorageService
{
    private readonly StorageClient _storageClient;
    private readonly UrlSigner _urlSigner;
    private const string BucketName = "photodrop-uploads";

    public CloudStorageService()
    {
        var clientEmail = Environment.GetEnvironmentVariable("GCS_CLIENT_EMAIL");
        var privateKey = Environment.GetEnvironmentVariable("GCS_PRIVATE_KEY");
        ServiceAccountCredential serviceAccountCredential;

        if (!string.IsNullOrWhiteSpace(clientEmail) && !string.IsNullOrWhiteSpace(privateKey))
        {
            // Production: build credential from individual values
            serviceAccountCredential = new ServiceAccountCredential(
                new ServiceAccountCredential.Initializer(clientEmail)
                {
                    ProjectId = "photodrop-489015"
                }.FromPrivateKey(privateKey));
        }
        else
        {
            // Development: load from local file
            var credentialPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Secrets", "service-account.json");
            serviceAccountCredential = ServiceAccountCredential.FromServiceAccountData(
                File.OpenRead(credentialPath));
        }

        var googleCredential = GoogleCredential.FromServiceAccountCredential(serviceAccountCredential);

        _storageClient = StorageClient.Create(googleCredential);
        _urlSigner = UrlSigner.FromCredential(serviceAccountCredential);
    }

    // Generates a temporary upload URL that lets a guest upload one file directly to Cloud Storage.
    // The URL expires after the specified duration — the guest never sees any credentials.
    public string GenerateUploadUrl(string objectName, string contentType, TimeSpan? expiry = null)
    {
        var duration = expiry ?? TimeSpan.FromMinutes(15);

        var requestTemplate = UrlSigner.RequestTemplate
            .FromBucket(BucketName)
            .WithObjectName(objectName)
            .WithHttpMethod(HttpMethod.Put)
             .WithRequestHeaders(new Dictionary<string, IEnumerable<string>>
        {
            { "Content-Type", new[] { contentType } }
        });

        var options = UrlSigner.Options.FromDuration(duration);

        return _urlSigner.Sign(requestTemplate, options);
    }

    // Downloads a file from Cloud Storage as a stream (used when moving files to Google Drive).
    public async Task<Stream> DownloadFileAsync(string objectName)
    {
        var stream = new MemoryStream();
        await _storageClient.DownloadObjectAsync(BucketName, objectName, stream);
        stream.Position = 0;
        return stream;
    }

    // Deletes a file from Cloud Storage after it's been moved to Google Drive.
    public async Task DeleteFileAsync(string objectName)
    {
        await _storageClient.DeleteObjectAsync(BucketName, objectName);
    }

    // Lists all files in the bucket with the given prefix (e.g., "eventId/")
    public async Task<List<string>> ListFilesAsync(string prefix)
    {
        var objects = _storageClient.ListObjectsAsync(BucketName, prefix);
        var fileNames = new List<string>();

        await foreach (var obj in objects)
        {
            fileNames.Add(obj.Name);
        }

        return fileNames;
    }
}