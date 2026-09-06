using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Utilities
{
    public interface ITransactionBuilderService
    {
        BuiltTransaction Build(
            IEnumerable<Utxo> utxos,
            IDictionary<string, string> privateKeyByAddress,
            string toAddress,
            long amountSats,
            string changeAddress,
            decimal feeRateSatsPerVByte,
            bool sweepAll,
            BitcoinNetwork network);
    }
}
