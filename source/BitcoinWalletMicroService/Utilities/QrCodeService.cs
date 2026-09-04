using QRCoder;

namespace BitcoinWalletMicroService.Utilities
{
    public class QrCodeService : IQrCodeService
    {
        private const QRCodeGenerator.ECCLevel EccLevel = QRCodeGenerator.ECCLevel.Q;

        private const int MinPixelsPerModule = 2;
        private const int MaxPixelsPerModule = 40;

        private readonly QRCodeGenerator _generator = new QRCodeGenerator();

        public byte[] GeneratePng(string payload, int pixelsPerModule = 10)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(payload);

            using (QRCodeData data = _generator.CreateQrCode(payload, EccLevel))
            {
                var png = new PngByteQRCode(data);

                return png.GetGraphic(Clamp(pixelsPerModule));
            }
        }

        public string GenerateSvg(string payload, int pixelsPerModule = 10)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(payload);

            using (QRCodeData data = _generator.CreateQrCode(payload, EccLevel))
            {
                var svg = new SvgQRCode(data);

                return svg.GetGraphic(Clamp(pixelsPerModule));
            }
        }


        private static int Clamp(int pixelsPerModule) =>
            Math.Clamp(pixelsPerModule, MinPixelsPerModule, MaxPixelsPerModule);
        
    }
}
