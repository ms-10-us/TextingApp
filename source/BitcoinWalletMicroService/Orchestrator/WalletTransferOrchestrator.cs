using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Mappings;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroService.Utilities;

namespace BitcoinWalletMicroService.Orchestrator
{
    public class WalletTransferOrchestrator : IWalletTransferOrchestrator
    {
        private const int DefaultFeeTargetBlocks = 6;

        private readonly IWalletRepository _repository = default!;
        private readonly IMnemonicService _mnemonicService = default!;
        private readonly IKeyDerivationService _keyDerivationService = default!;
        private readonly ISercretProtector _secretProtector = default!;
        private readonly IBlockstreamClient _blockstreamClient = default!;
        private readonly ITransactionBuilderService _transactionBuilder = default!;

        public async Task<ReceiveAddressResult> GetNextReceiveAddressAsync(
            string walletId,
            bool isChange = false,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(walletId);

            WalletEntity? wallet = await _repository.GetWalletByIdAsync(walletId, ct).ConfigureAwait(false);

            if (wallet == null)
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' was not found.");
            }

            int nextIndex = await _repository
                .GetNextAddressIndexAsync(walletId, isChange, ct)
                .ConfigureAwait(false);

            BitcoinNetwork network = ParseNetwork(wallet.Network);
            AddressType addressType = ParseAddressType(wallet.AccountDerivationPath);

            DerivedKeyResult derived = _keyDerivationService.DerivePublicKeys(
                wallet.AccountExtendedPublicKey, network, addressType, isChange, nextIndex, 1).Single();

            await _repository
                .InsertDerivedKeyAsync([derived.ToEntity(walletId, DateTime.UtcNow)], null, ct)
                .ConfigureAwait(false);

            return new ReceiveAddressResult
            {
                Address = derived.Address,
                DerivationPath = derived.DerivationPath,
                AddressIndex = derived.AddressIndex,
                IsChange = derived.IsChange,
                PaymentUri = BipUriBuilder.Build(derived.Address)
            };
        }

        private static BitcoinNetwork ParseNetwork(string value) =>
            string.Equals(value, "TestNet", StringComparison.OrdinalIgnoreCase)
            ? BitcoinNetwork.TestNet
            : BitcoinNetwork.Main;

        private static AddressType ParseAddressType(string accountDerivationPath)
        {
            if (!string.IsNullOrEmpty(accountDerivationPath))
            {
                if (accountDerivationPath.StartsWith("m/44'", StringComparison.Ordinal))
                {
                    return AddressType.Legacy;
                }

                if (accountDerivationPath.StartsWith("m/49'", StringComparison.Ordinal))
                {
                    return AddressType.NestedSegwit;
                }

                if (accountDerivationPath.StartsWith("m/84'", StringComparison.Ordinal))
                {
                    return AddressType.NativeSegwit;
                }
            }

            throw new InvalidOperationException(
                $"Cannot determine address type from path '{accountDerivationPath}'.");
        }
    }
}
