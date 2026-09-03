namespace BitcoinWalletMicroService.Models
{
    public class WalletSummary
    {
        public string WalletId { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Network { get; set; } = string.Empty;

        public string AccountExtendedPublicKey { get; set; } = string.Empty;

        public string AccountDerivationPath { get; set; } = string.Empty;

        public int AddressCount { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
