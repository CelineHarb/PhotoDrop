using System.ComponentModel.DataAnnotations;

namespace PhotoDrop.Models;

public class GuestSession
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventId { get; set; } = "";
    public string SessionToken { get; set; } = "";
    public int PhotosUploaded { get; set; } = 0;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUploadAt { get; set; } = DateTime.UtcNow;
}