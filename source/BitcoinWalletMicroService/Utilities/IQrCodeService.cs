namespace BitcoinWalletMicroService.Utilities
{
    public interface IQrCodeService
    {
        byte[] GeneratePng(string payload, int pixelsPerModule = 10);

        string GenerateSvg(string payload, int pixelsPerModule = 10);
    }
}
