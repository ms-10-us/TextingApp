namespace BitcoinWalletMicroService.Models
{
    public class DerivedKeyResult
    {
        public string DerivationPath { get; set; } = string.Empty;
        public int AddressIndex { get; set; }
        public bool IsChange {  get; set; }
        public string Address { get; set; } = string.Empty;
        public string PublicKeyHex { get; set; } = string.Empty;

        public string? PrivateKeyWif { get; set; }
    }
}
