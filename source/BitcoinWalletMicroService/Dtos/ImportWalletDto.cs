using BitcoinWalletMicroService.Enums;

namespace BitcoinWalletMicroService.Dtos
{
    public class ImportWalletDto
    {
        public string Label { get; set; } = string.Empty;

        public string Mnemonic { get; set; } = string.Empty;

        public string? Passphrase { get; set; }

        public BitcoinNetwork Network { get; set; } = BitcoinNetwork.TestNet;

        public AddressType AddressType { get; set; } = AddressType.NativeSegwit;

        public int InitalAddressCount { get; set; } = 1;
    }
}
