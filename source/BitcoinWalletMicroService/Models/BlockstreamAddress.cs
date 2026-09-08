using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BitcoinWalletMicroService.Models
{
    public class BlockstreamAddress
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("chain_stats")]
        public BlockstreamAddressStats ChainStats { get; set; }

        [JsonPropertyName("mempool_stats")]
        public BlockstreamAddressStats MempoolStats { get; set; }
    }
}
