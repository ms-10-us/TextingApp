namespace BitcoinWalletMicroService.Entities
{
    public class WalletEntity
    {
        public required string Id { get; set; }

        public required string Label { get; set; }

        public required string Network {  get; set; }

        public required byte[] EncryptedMnemonic { get; set; }

        public required string MnemonicFingerprint { get; set; }

        public required string AccountExtendedPublicKey { get; set; }

        public required string AccountDerivationPath { get; set; }

        public required DateTime CreatedUtc { get; set; }
    }
}
