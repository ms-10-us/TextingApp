namespace BitcoinWalletMicroService.Dtos
{
    public class WalletBalanceDtoResponse
    {
        public required string WalletId { get; set; }

        public required long ConfirmedSats { get; set; }

        public required long UnconfimredSats { get; set; }

        public required long TotalSats { get; set; }

        public required int UtxoCount { get; set; }
    }
}
