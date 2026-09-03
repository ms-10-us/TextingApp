using BitcoinWalletMicroService.Enums;

namespace BitcoinWalletMicroService.Models
{
    public class ImportWalletModel
    {
        public string Label { get; set; } = string.Empty;

        public string Mnemonic { get; set; } = string.Empty;

        public string? Passphrase { get; set; }

        public BitcoinNetwork Network { get; set; }

        public AddressType AddressType { get; set; }

        public int InitalAddressCount { get; set; }
    }
}
