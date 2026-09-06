namespace BitcoinWalletMicroService.Models
{
    public class BuiltTransaction
    {
        public required string RawHex { get; set; }

        public required string TxId { get; set; }

        public required long FeeSats { get; set; }

        public required long AmountSats { get; set; }

        public required long ChangeSats { get; set; }

        public required int VirtualSizeBytes { get; set; }

        public required int InputCount { get; set; }
    }
}
