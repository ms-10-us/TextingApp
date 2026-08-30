using BitcoinWalletMicroService.Enums;

namespace BitcoinWalletMicroService.Dtos
{
    public class CreateWalletDto
    {
        public string Label { get; set; } = string.Empty;
        public MnemonicStrength Strength { get; set; } = MnemonicStrength.Words12;
        public BitcoinNetwork Network { get; set; } = BitcoinNetwork.TestNet;
        public AddressType AddressType { get; set; } = AddressType.NativeSegwit;

        public string? Passphrase { get; set; }

        public int InitialAddressCount { get; set; } = 1;
    }
}
