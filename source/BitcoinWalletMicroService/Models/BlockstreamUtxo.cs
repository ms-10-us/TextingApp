using BitcoinWalletMicroService.Utilities;
using System.Text.Json.Serialization;

namespace BitcoinWalletMicroService.Models
{
    public class BlockstreamUtxo
    {
        [JsonPropertyName("txid")]
        public string TxId { get; set; } = string.Empty;

        [JsonPropertyName("vout")]
        public int Vout { get; set; }

        [JsonPropertyName("value")]
        public long Value { get; set; }

        [JsonPropertyName("status")]
        public BlockstreamStatus? Status { get; set; }
    }
}
