
namespace PhotoDrop.Models;
public class DriveStorageInfo
{
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }

    // Helper to estimate how many photos can fit (assuming ~5MB average per photo)
    public int EstimatedPhotoCapacity => (int)(AvailableBytes / (5 * 1024 * 1024));

    public string AvailableFormatted => FormatBytes(AvailableBytes);
    public string TotalFormatted => FormatBytes(TotalBytes);
    public string UsedFormatted => FormatBytes(UsedBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}