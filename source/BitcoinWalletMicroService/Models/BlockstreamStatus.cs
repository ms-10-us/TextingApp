using System.Text.Json.Serialization;

namespace BitcoinWalletMicroService.Models
{
    public class BlockstreamStatus
    {
        [JsonPropertyName("confirmed")]
        public bool Confirmed { get; set; }

        [JsonPropertyName("block_height")]
        public int? BlockHeight { get; set; }
    }
}
