using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Utilities
{
    public interface IBlockstreamClient
    {
        Task<IEnumerable<Utxo>> GetUtxoAsync(
            string address, bool isChange, int addressIndex, BitcoinNetwork network, CancellationToken ct = default);

        Task<decimal> GetFeeRateAsync(BitcoinNetwork network, int targetBlocks = 6, CancellationToken ct = default);

        Task<string> BroadcastAsync(string rawTransactionHex, BitcoinNetwork network, CancellationToken ct = default);
    }
}
