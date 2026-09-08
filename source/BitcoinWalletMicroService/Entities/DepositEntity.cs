namespace BitcoinWalletMicroService.Entities
{
    public class DepositEntity
    {
        public long Id { get; set; }

        public required string DepositId { get; set; }

        public required string WalletId { get; set; }

        public required string Address { get; set; }

        public required int AddressIndex { get; set; }

        public required bool IsChange { get; set; }

        public long? ExpectedSats { get; set; }

        public string? Label { get; set; }

        public required long ReceivedSats { get; set; }

        public required string Status { get; set; }

        public required string? TxId {  get; set; }

        public required DateTime CreatedUtc { get; set; }

        public required DateTime ExpiresUtc { get; set; }

        public DateTime? ConfirmedUtc { get; set; }
    }
}
