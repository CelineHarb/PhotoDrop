namespace PhotoDrop.Models
{
    public record UploadUrlRequest(string FileName, string ContentType);
    public class EventRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventName { get; set; } = "";
        public string FolderId { get; set; } = "";
        public string GuestToken { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
