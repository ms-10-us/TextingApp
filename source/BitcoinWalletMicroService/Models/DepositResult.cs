using BitcoinWalletMicroService.Enums;

namespace BitcoinWalletMicroService.Models
{
    public class DepositResult
    {
        public required string DepositId {  get; set; }
        public required string WalletId { get; set; }

        public required string Address { get; set; }

        public required string PaymentUri { get; set; }

        public long? ExpectedSats { get; set; }
        public required long ReceivedSats { get; set; }
        public required long UnconfirmedSats { get; set; }
        public required DepositStatus Status { get; set; }
        public required int Confirmations { get; set; }
        public string? TxId { get; set; }
        public required DateTime CreatedUtc { get; set; }
        public required DateTime ExpiresUtc { get; set; }
        public DateTime? ConfirmedUtc { get; set; }
    }
}
