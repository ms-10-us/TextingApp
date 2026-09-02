namespace BitcoinWalletMicroService.Entities
{
    public class DerivedKeyEntity
    {
        public long Id { get; set; }

        public required string WalletId { get; set; }

        public required int IsChange { get; set; }

        public required int AddressIndex {  get; set; }

        public required string DerivationPath { get; set; }

        public required string PublicKeyHex { get; set; }

        public required string Address {  get; set; }

        public required DateTime CreatedUtc { get; set; }
    }
}
