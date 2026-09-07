namespace BitcoinWalletMicroService.Models
{
    public class WalletBalance
    {
        public required string WalletId { get; set; }

        public required long ConfirmedSats { get; set; }

        public required long UncofirmedSats { get; set; }

        public required long TotalSats { get; set; }

        public required int UtxoCount { get; set; }
    }
}
