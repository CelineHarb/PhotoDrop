using QRCoder;

namespace PhotoDrop.Services
{
    public class QrCodeService
    {
        // Generates a QR code PNG
        public string GenerateDataUri( string url, int pixelsPerModule = 10)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            var pngBytes = qrCode.GetGraphic(pixelsPerModule);
            return $"data:image/png;base64, {Convert.ToBase64String(pngBytes)}";
        }

        // Generate raw PNG bytes, used for download endpoint 
        public byte[] GeneratePng(string url, int pixelsPerModule = 10)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            return qrCode.GetGraphic(pixelsPerModule);
        }
    }
}
