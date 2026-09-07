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
        private readonly IWalletTransferRepository _transferRepository = default!;
        private readonly IMnemonicService _mnemonicService = default!;
        private readonly IKeyDerivationService _keyDerivationService = default!;
        private readonly ISercretProtector _secretProtector = default!;
        private readonly IBlockstreamClient _blockstreamClient = default!;
        private readonly ITransactionBuilderService _transactionBuilder = default!;
        private readonly ILogger<WalletTransferOrchestrator> _logger = default!;

        public WalletTransferOrchestrator(
            IWalletRepository repository,
            IWalletTransferRepository transferRepository,
            IMnemonicService mnemonicService,
            IKeyDerivationService keyDerivationService,
            ISercretProtector sercretProtector,
            IBlockstreamClient blockstreamClient,
            ITransactionBuilderService transactionBuilder,
            ILogger<WalletTransferOrchestrator> logger)
        {
            _repository = repository;
            _transferRepository = transferRepository;
            _mnemonicService = mnemonicService;
            _keyDerivationService = keyDerivationService;
            _secretProtector = sercretProtector;
            _blockstreamClient = blockstreamClient;
            _transactionBuilder = transactionBuilder;
            _logger = logger;
        }


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

        public async Task<WalletBalance> GetBalanceAsync(string walletId, CancellationToken ct = default)
        {
            IEnumerable<Utxo> utxos = await GetWalletUtxosAsync(walletId, ct).ConfigureAwait(false);

            long confirmed = utxos.Where(u => u.IsConfirmed).Sum(u => u.ValueSats);
            long unconfirmed = utxos.Where(u => !u.IsConfirmed).Sum(u => u.ValueSats);

            return new WalletBalance
            {
                WalletId = walletId,
                ConfirmedSats = confirmed,
                UncofirmedSats = unconfirmed,
                TotalSats = confirmed + unconfirmed,
                UtxoCount = utxos.Count()
            };
        }

        public async Task<SendResult> SendAsync(SendModel model, CancellationToken ct = default(CancellationToken))
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (string.IsNullOrWhiteSpace(model.ToAddress))
            {
                throw new ArgumentException("Destination address is required.", "model");
            }

            if (!model.SweepAll && model.AmountSats <= 0)
            {
                throw new ArgumentException("Amount must be greater tan zero.", "model");
            }

            if (!model.DryRun && string.IsNullOrWhiteSpace(model.IdempotencyKey))
            {
                throw new ArgumentException("Idempotency is required when DryRun is false.", "model");
            }

            if (!model.DryRun)
            {
                SendTransactionEntity? alreadySent = await _transferRepository
                    .GetSendTransactionByIdempotencyKeyAsync(model.WalletId, model.IdempotencyKey!, ct)
                    .ConfigureAwait(false);

                if (alreadySent != null)
                {
                    _logger.LogInformation(
                        "Idempotent replay for key {Key}; returning existing transaction {TxId}.",
                        model.IdempotencyKey, alreadySent.TxId);
                }

                return alreadySent.ToResult();
            }

            WalletEntity wallet = await _repository.GetWalletByIdAsync(model.WalletId, ct)
                .ConfigureAwait(false);

            if (wallet == null)
            {
                throw new KeyNotFoundException($"Wallet {model.WalletId} was not found.");
            }

            BitcoinNetwork network = ParseNetwork(wallet.Network);
            AddressType addressType = ParseAddressType(wallet.AccountDerivationPath);

            IEnumerable<Utxo> utxos = await GetWalletUtxosAsync(model.WalletId, ct)
                .ConfigureAwait(false);

            decimal feeRate = model.FeeSats
                ?? await _blockstreamClient.GetFeeRateAsync(network, DefaultFeeTargetBlocks, ct)
                .ConfigureAwait(false);

            ReceiveAddressResult changeAddress = await GetNextReceiveAddressAsync(model.WalletId, true, ct)
                .ConfigureAwait(false);

            byte[]? seed = null;
            Dictionary<string, string> keysByAddress = new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {

                string mnemonic = _secretProtector.Unprotect(wallet.EncryptedMnemonic);
                seed = _mnemonicService.ToSeed(mnemonic, model.Passphrase ?? string.Empty);

                AccountKeys account = _keyDerivationService.DeriveAccount(seed, network, addressType, 0);

                if (!string.Equals(account.AccountExtendedPublicKey, wallet.AccountExtendedPublicKey, StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException(
                        "Passphrase does not match the one used when this wallet was created.");
                }

                foreach (Utxo utxo in utxos)
                {
                    DerivedKeyResult key = _keyDerivationService.DerivePrivateKeys(
                        seed, network, addressType, 0, utxo.IsChange, utxo.AddressIndex);

                    if (!string.IsNullOrEmpty(key.PrivateKeyWif))
                    {
                        keysByAddress[utxo.Address] = key.PrivateKeyWif;
                    }
                }

                BuiltTransaction built = _transactionBuilder.Build(
                    utxos, keysByAddress, model.ToAddress, model.AmountSats, changeAddress.Address, feeRate, model.SweepAll, network);

                SendResult result = new SendResult
                {
                    TxId = null,
                    RawTransactionHex = built.RawHex,
                    AmountSats = built.AmountSats,
                    FeeSats = built.FeeSats,
                    FeeRateSatsPerVByte = feeRate,
                    VirtualSizeBytes = built.VirtualSizeBytes,
                    ToAddress = model.ToAddress,
                    ChangeAddress = built.ChangeSats > 0 ? changeAddress.Address : null,
                    ChangeSats = built.ChangeSats,
                    InputCount = built.InputCount,
                    Broadcast = false,
                    WasIdempotentReplay = false
                };

                if (model.DryRun)
                {
                    return result;
                }

                string txId = await _blockstreamClient.BroadcastAsync(built.RawHex, network, ct).ConfigureAwait(false);
                result.TxId = txId;
                result.Broadcast = true;

                try
                {
                    SendTransactionEntity entity = result.ToEntity(model.WalletId, model.IdempotencyKey!);
                    await _transferRepository.InsertSentTransactionAsync(entity, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Transaction {TxId} was BROADCAST but could not be recorded for wallet {WalletId}. " +
                        "Idempotency is not protected for key {Key} - a retry would double spend.",
                        txId, model.WalletId, model.IdempotencyKey);
                }

                _logger.LogInformation(
                   "Broadcast {TxId}: {Amount} sats to {ToAddress}, fee {Fee} sats.",
                   txId, result.AmountSats, model.ToAddress, result.FeeSats);

                return result;
            }
            finally
            {
                if (seed != null)
                {
                    Array.Clear(seed, 0, seed.Length);
                }

                keysByAddress.Clear();
            }

        }

        public async Task<IEnumerable<SendTransaction>> GetSendTransactionAsync(
            string walletId, CancellationToken ct)
        {
            IEnumerable<SendTransactionEntity> entities = await _transferRepository
                .GetSendTransactionAsync(walletId, ct).ConfigureAwait(false);

            List<SendTransaction> result = new List<SendTransaction>();
            foreach(SendTransactionEntity entity in entities)
            {
                result.Add(entity.ToModel());
            }

            return result;
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

        private async Task<IEnumerable<Utxo>> GetWalletUtxosAsync(string walletId, CancellationToken ct)
        {
            WalletEntity? wallet = await _repository.GetWalletByIdAsync(walletId, ct).ConfigureAwait(false);

            if (wallet == null)
            {
                throw new KeyNotFoundException($"Wallet '{walletId}' was not found.");
            }

            BitcoinNetwork network = ParseNetwork(wallet.Network);

            IEnumerable<DerivedKeyEntity> keys = await _repository
                .GetDerivedKeysAsync(walletId, null, ct)
                .ConfigureAwait(false);

            var utxos = new List<Utxo>();

            foreach (DerivedKeyEntity key in keys)
            {
                IEnumerable<Utxo> found = await _blockstreamClient
                    .GetUtxoAsync(key.Address, key.IsChange, key.AddressIndex, network, ct)
                    .ConfigureAwait(false);

                utxos.AddRange(found);
            }

            return utxos;
        }
    }
}
