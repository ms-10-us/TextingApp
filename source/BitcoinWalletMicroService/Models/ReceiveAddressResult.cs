namespace BitcoinWalletMicroService.Models
{
    public class ReceiveAddressResult
    {
        public required string Address { get; set; }
        public required string DerivationPath { get; set; }
        public required int AddressIndex { get; set; }
        public required bool IsChange {  get; set; }

        public required string PaymentUri { get; set; }
    }
}
