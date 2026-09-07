using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using NBitcoin;
using NBitcoin.Policy;

namespace BitcoinWalletMicroService.Utilities
{
    public class TransactionBuilderService : ITransactionBuilderService
    {
        private const long DustThresholdSats = 294;

        public BuiltTransaction Build(
            IEnumerable<Utxo> utxos, 
            IDictionary<string, string> privateKeyByAddress, 
            string toAddress, 
            long amountSats, 
            string changeAddress, 
            decimal feeRateSatsPerVByte, 
            bool sweepAll, 
            BitcoinNetwork network)
        {
            ArgumentNullException.ThrowIfNull(utxos);
            ArgumentNullException.ThrowIfNull(privateKeyByAddress);
            ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);
            ArgumentException.ThrowIfNullOrWhiteSpace(changeAddress);

            Network net = network == BitcoinNetwork.Main ? Network.Main : Network.TestNet;

            List<Utxo> spendable = utxos.Where(u => u.IsConfirmed).ToList();

            if (spendable.Count == 0)
            {
                throw new InvalidOperationException(
                    "No confirmed outputs available. Uncofirmed funds cannot be spent safely.");
            }

            BitcoinAddress destination;
            try
            {
                destination = BitcoinAddress.Create(toAddress, net);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(
                    $"'{toAddress}' is not a valid {net} address. " +
                    "Check you are not mixing mainnet and testnet addresses.", nameof(toAddress), ex);
            }

            BitcoinAddress change = BitcoinAddress.Create(changeAddress, net);

            var builder = net.CreateTransactionBuilder();
            long totalAvailable = 0;

            foreach(Utxo utxo in spendable)
            {
                if (!privateKeyByAddress.TryGetValue(utxo.Address, out string? wif))
                {
                    throw new InvalidOperationException(
                        $"No signing key for address {utxo.Address}. The wallet cannot spend this output.");
                }

                BitcoinSecret secret = net.CreateBitcoinSecret(wif);
                Script scriptPubKey = BitcoinAddress.Create(utxo.Address, net).ScriptPubKey;

                var coin = new Coin(
                    fromTxHash: uint256.Parse(utxo.TxId),
                    fromOutputIndex: (uint)utxo.Vout,
                    amount: Money.Satoshis(utxo.ValueSats),
                    scriptPubKey: scriptPubKey);

                builder.AddCoin(coin);
                builder.AddKeys(secret);

                totalAvailable += utxo.ValueSats;
            }

            var feeRate = new FeeRate(Money.Satoshis((long)Math.Ceiling(feeRateSatsPerVByte * 1000)));

            if (sweepAll)
            {
                builder.SendAll(destination);
                builder.SubtractFees();
            }
            else
            {
                if (amountSats < DustThresholdSats)
                {
                    throw new ArgumentException(
                        $"Amount {amountSats} sats is below the dust threshold of {DustThresholdSats} sats. " +
                        "Nodes will not relay it.", nameof(amountSats));
                }

                builder.Send(destination, Money.Satoshis(amountSats));
                builder.SetChange(change);
            }

            builder.SendEstimatedFees(feeRate);
            Transaction tx;

            try
            {
                tx = builder.BuildTransaction(sign: true);
            }
            catch (NotEnoughFundsException ex)
            {
                throw new InvalidOperationException(
                    $"Insufficient funds. Available {totalAvailable} sats, " +
                    $"needed {amountSats} sats plus fees. {ex.Message}", ex);
            }

            if (!builder.Verify(tx, out TransactionPolicyError[] errors))
            {
                throw new InvalidOperationException(
                   "Built transaction failed verification: " +
                   string.Join("; ", errors.Select(e => e.ToString())));
            }

            Money fee = tx.GetFee([.. builder.FindSpentCoins(tx)]);
            int vsize = tx.GetVirtualSize();

            long changeSats = tx.Outputs
                .Where(o => o.ScriptPubKey == change.ScriptPubKey)
                .Sum(o => o.Value.Satoshi);

            long sentSats = tx.Outputs
                .Where(o => o.ScriptPubKey == destination.ScriptPubKey)
                .Sum(o => o.Value.Satoshi);

            return new BuiltTransaction
            {
                RawHex = tx.ToHex(),
                TxId = tx.GetHash().ToString(),
                FeeSats = fee?.Satoshi ?? 0,
                AmountSats = sentSats,
                ChangeSats = changeSats,
                VirtualSizeBytes = vsize,
                InputCount = tx.Inputs.Count
            };
        }
    }
}
