namespace BitcoinWalletMicroService.Entities
{
    public class SendTransactionEntity
    {
        public long Id { get; set; }

        public required string TxId { get; set; }

        public required string WalletId { get; set; }

        public required string IdempotencyKey { get; set; }

        public required string ToAddress { get; set; }

        public required long AmountSats { get; set; }

        public required long FeeSats { get; set; }

        public required int VirtualSizeBytes { get; set; }

        public required int InputCount { get; set; }

        public string? ChangeAddress { get; set; }

        public required long ChangeSats { get; set; }

        public required string RawTransactionHex { get; set; }

        public required DateTime BroadcastUtc { get; set; }
    }
}
