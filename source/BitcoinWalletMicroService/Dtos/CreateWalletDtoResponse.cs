using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Dtos
{
    public class CreateWalletDtoResponse
    {
        public string WalletId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Mnemonic { get; set; } = string.Empty;
        public string AccountExtendedPublicKey { get; set; } = string.Empty;
        public string AccountDerivationPath { get; set; } = string.Empty;
        public IEnumerable<DerivedKeyResult> Addresses { get; set; } = Array.Empty<DerivedKeyResult>();
        public DateTime CreatedUtc { get; set; }
    }
}
