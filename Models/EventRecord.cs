using System.ComponentModel.DataAnnotations;
namespace PhotoDrop.Models
{
    public record UploadUrlRequest(string FileName, string ContentType);
    public class EventRecord
    {
        [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventName { get; set; } = "";
        public string FolderId { get; set; } = "";
        public string GuestToken { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string? RefreshToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public long StorageLimitBytes { get; set; } = 0; // 0 = no limit set yet
        public long StorageUsedBytes { get; set; } = 0;
        public int PhotoCount { get; set; } = 0;
        public int? PhotoLimit { get; set; } // null = no limit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
