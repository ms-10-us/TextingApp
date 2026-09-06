namespace BitcoinWalletMicroService.Models
{
    public class Utxo
    {
        public required string TxId { get; set; }

        public required int Vout {  get; set; }

        public required long ValueSats { get; set;  }

        public required string Address { get; set; }

        public required bool IsChange { get; set; }

        public required int AddressIndex { get; set; }

        public required bool IsConfirmed { get; set; }

        public int? BlockHeight {  get; set; }
    }
}
