using BitcoinWalletMicroService.Enums;

namespace BitcoinWalletMicroService.Models
{
    public class CreateWalletModel
    {
        public required string Label { get; set; }
        public MnemonicStrength Strength { get; set; }
        public BitcoinNetwork Network { get; set; }
        public AddressType AddressType { get; set; }

        public string? Passphrase { get; set; }

        public int InitialAddressCount { get; set; }
    }
}
