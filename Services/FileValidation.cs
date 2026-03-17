namespace PhotoDrop.Services
{
    public class FileValidation
    {
        public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/heic", "image/heif", "image/webp"
    };

        public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp"
    };

        public const long MaxFileSize = 15 * 1024 * 1024;
        public const int MaxFilesPerRequest = 20;

        // Reads the first bytes of a file to verify it's actually an image, since file extensions and content types can be faked.
        public static bool IsValidImageHeader(byte[] header, int length)
        {
            if (length < 3) return false;

            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            if (length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return true;

            if (length >= 12
                && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            if (length >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
                return true;

            return false;
        }
    }
}
