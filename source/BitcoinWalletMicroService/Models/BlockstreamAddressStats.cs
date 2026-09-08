using System.Text.Json.Serialization;

namespace BitcoinWalletMicroService.Models
{
    public class BlockstreamAddressStats
    {
        [JsonPropertyName("funded_txo_count")]
        public int FundedTxoCount { get; set; }

        [JsonPropertyName("funded_txo_sum")]
        public long FundedTxoSum { get; set; }

        [JsonPropertyName("spent_txo_count")]
        public int SpentTxoCount { get; set; }

        [JsonPropertyName("spent_txo_sum")]
        public long SpentTxoSum { get; set; }

        [JsonPropertyName("tx_count")]
        public int TxCount { get; set; }
    }
}
