namespace BitcoinWalletMicroService.Models
{
    public class AddressStats
    {
        public required string Address { get; set; }

        public required long ConfirmedReceivedSats { get; set; }

        public required long UnconfirmedReceivedSats { get; set; }

        public required int ConfirmedTxCount { get; set; }

        public required int UnconfirmedTxCount { get; set; }

        public long TotalReceivedSats => ConfirmedReceivedSats + UnconfirmedReceivedSats;

        public bool HasActivity => ConfirmedTxCount > 0 || UnconfirmedTxCount > 0;
    }
}
