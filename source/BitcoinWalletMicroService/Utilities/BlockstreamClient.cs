using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace BitcoinWalletMicroService.Utilities
{
    public class BlockstreamClient : IBlockstreamClient
    {
        private const string MainnetBaseUrl = "https://blockstream.info/api";
        private const string TestnetBaseUrl = "https://blockstream.info/testnet/api";

        private const decimal MinimumFeeRate = 1.0m;

        private readonly HttpClient _httpClient;
        private readonly ILogger<BlockstreamClient> _logger;

        public BlockstreamClient(HttpClient httpClient, ILogger<BlockstreamClient> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IEnumerable<Utxo>> GetUtxoAsync(
            string address, 
            bool isChange, 
            int addressIndex, 
            BitcoinNetwork network, 
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(address);

            string url = $"{BaseUrl(network)}/address/{address}/utxo";

            List<BlockstreamUtxo>? raw = await _httpClient
                .GetFromJsonAsync<List<BlockstreamUtxo>>(url, ct)
                .ConfigureAwait(false);

            if (raw == null)
            {
                return [];
            }

            return raw.Select(u => new Utxo
            {
                TxId = u.TxId,
                Vout = u.Vout,
                ValueSats = u.Value,
                Address = address,
                IsChange = isChange,
                AddressIndex = addressIndex,
                IsConfirmed = u.Status?.Confirmed ?? false,
                BlockHeight = u.Status?.BlockHeight
            }).ToList();
        }

        public async Task<decimal> GetFeeRateAsync(
            BitcoinNetwork network, 
            int targetBlocks = 6, 
            CancellationToken ct = default)
        {
            try
            {
                Dictionary<string, decimal>? estimates = await _httpClient
                    .GetFromJsonAsync<Dictionary<string, decimal>>($"{BaseUrl(network)}/fee-estimates", ct)
                    .ConfigureAwait(false);

                if (estimates != null)
                {
                    estimates.TryGetValue(targetBlocks.ToString(CultureInfo.InvariantCulture), out decimal rate);

                    if (rate > 0)
                    {
                        return Math.Max(rate, MinimumFeeRate);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Fee estimate unavailable; falling back to {rate} sar/vB.", MinimumFeeRate);
            }

            return MinimumFeeRate;
        }

        public async Task<string> BroadcastAsync(
            string rawTransactionHex, 
            BitcoinNetwork network, 
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawTransactionHex);

            using var content = new StringContent(rawTransactionHex, Encoding.UTF8, "text/plain");
            {
                HttpResponseMessage response = await _httpClient
                    .PostAsync($"{BaseUrl(network)}/tx", content, ct)
                    .ConfigureAwait(false);

                string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Broadcase rejected ({(int)response.StatusCode}): {body}");
                }

                return body.Trim();
            }
        }

        public async Task<AddressStats> GetAddressStatsAsync(
            string address, BitcoinNetwork network, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(address);

            string url = $"{BaseUrl(network)}/address/{address}";

            BlockstreamAddress? raw = await _httpClient
                .GetFromJsonAsync<BlockstreamAddress>(url, ct)
                .ConfigureAwait(false);

            if (raw == null)
            {
                return new AddressStats
                {
                    Address = address,
                    ConfirmedReceivedSats = 0,
                    UnconfirmedReceivedSats = 0,
                    ConfirmedTxCount = 0,
                    UnconfirmedTxCount = 0
                };
            }

            return new AddressStats
            {
                Address = address,
                ConfirmedReceivedSats = raw.ChainStats?.FundedTxoSum ?? 0,
                UnconfirmedReceivedSats = raw.MempoolStats?.FundedTxoSum ?? 0,
                ConfirmedTxCount = raw.ChainStats?.TxCount ?? 0,
                UnconfirmedTxCount = raw.MempoolStats?.TxCount ?? 0
            };
        }

        public async Task<int> GetBlockHeightAsync(BitcoinNetwork network, CancellationToken ct = default)
        {
            string text = await _httpClient
                .GetStringAsync($"{BaseUrl(network)}/blocks/tip/height", ct)
                .ConfigureAwait(false);

            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
            {
                throw new InvalidOperationException(
                    $"Unexpected response from tip height endpoint: '{text}'.");
            }

            return height;
        }

        private static string BaseUrl(BitcoinNetwork network) =>
            network == BitcoinNetwork.Main ? MainnetBaseUrl : TestnetBaseUrl;        
    }
}
