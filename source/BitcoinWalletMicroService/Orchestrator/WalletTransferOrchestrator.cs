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
        private const int RequiredConfirmations = 6;

        private readonly IWalletRepository _repository = default!;
        private readonly IWalletTransferRepository _transferRepository = default!;
        private readonly IMnemonicService _mnemonicService = default!;
        private readonly IKeyDerivationService _keyDerivationService = default!;
        private readonly ISercretProtector _secretProtector = default!;
        private readonly IBlockstreamClient _blockstreamClient = default!;
        private readonly ITransactionBuilderService _transactionBuilder = default!;
        private readonly ILogger<WalletTransferOrchestrator> _logger = default!;
        private readonly TimeProvider _timeProvider = default!;

        public WalletTransferOrchestrator(
            IWalletRepository repository,
            IWalletTransferRepository transferRepository,
            IMnemonicService mnemonicService,
            IKeyDerivationService keyDerivationService,
            ISercretProtector sercretProtector,
            IBlockstreamClient blockstreamClient,
            ITransactionBuilderService transactionBuilder,
            ILogger<WalletTransferOrchestrator> logger,
            TimeProvider timeProvider)
        {
            _repository = repository;
            _transferRepository = transferRepository;
            _mnemonicService = mnemonicService;
            _keyDerivationService = keyDerivationService;
            _secretProtector = sercretProtector;
            _blockstreamClient = blockstreamClient;
            _transactionBuilder = transactionBuilder;
            _logger = logger;
            _timeProvider = timeProvider;
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

                    return alreadySent.ToResult();
                }
                                
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

        public async Task<DepositResult> CreateDepositeAsync(
            CreateDepositModel model,
            CancellationToken ct = default(CancellationToken))
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (model.ExpectedSats.HasValue && model.ExpectedSats.Value < 294)
            {
                throw new ArgumentException("Expected amount is below the dust threshold of 294 sats; such a payment cannot be relayed.",
                    "model");
            }

            WalletEntity wallet = await _repository.GetWalletByIdAsync(model.WalletId, ct).ConfigureAwait(false);

            if (wallet == null)
            {
                throw new KeyNotFoundException($"Wallet {model.WalletId} was not found");
            }

            BitcoinNetwork network = ParseNetwork(wallet.Network);
            AddressType addressType = ParseAddressType(wallet.AccountDerivationPath);

            int nextIndex = await _repository.GetNextAddressIndexAsync(model.WalletId, false, ct).ConfigureAwait(false);

            DerivedKeyResult derived = _keyDerivationService.DerivePublicKeys(wallet.AccountExtendedPublicKey, network,
                addressType, false, nextIndex, 1).Single();

            DateTime now = DateTime.UtcNow;

            await _repository.InsertDerivedKeyAsync([derived.ToEntity(model.WalletId, now)], null, ct).ConfigureAwait(false);

            DepositEntity entity = new DepositEntity
            {
                DepositId = Guid.NewGuid().ToString("D"),
                WalletId = model.WalletId,
                Address = derived.Address,
                AddressIndex = derived.AddressIndex,
                IsChange = derived.IsChange,
                ExpectedSats = model.ExpectedSats,
                Label = model.Label,
                ReceivedSats = 0,
                Status = DepositStatus.Pending.ToString(),
                TxId = null,
                CreatedUtc = now,
                ExpiresUtc = now.AddMinutes(model.ExpiryMinutes),
                ConfirmedUtc = null
            };

            await _transferRepository.InsertDepositAsync(entity, ct).ConfigureAwait(false);

            _logger.LogInformation("Deposit {DepositId} created for wallet {WalletId} at {Address}, expecting {Expected} sats.",
                entity.DepositId, model.WalletId, entity.Address, model.ExpectedSats);

            return entity.ToResult(BuildPaymnetUri(entity), unconfirmedSats: 0, confirmations: 0);
        }

        public async Task<DepositResult> GetDepositAsync(
            string walletId,
            string depositId,
            CancellationToken ct = default(CancellationToken))
        {
            DepositEntity? deposit = await _transferRepository
                .GetDepositByIdAsync(walletId, depositId, ct)
                .ConfigureAwait(false);

            if (deposit == null)
            {
                throw new KeyNotFoundException($"Deposit {depositId} was not found");
            }

            WalletEntity wallet = await _repository
                .GetWalletByIdAsync(walletId, ct)
                .ConfigureAwait(false);

            BitcoinNetwork network = ParseNetwork(wallet.Network);

            AddressStats stats = await _blockstreamClient
                .GetAddressStatsAsync(deposit.Address, network, ct)
                .ConfigureAwait(false);

            int confirmations = 0;

            if (stats.ConfirmedReceivedSats > 0)
            {
                IEnumerable<Utxo> utxos = await _blockstreamClient
                    .GetUtxoAsync(deposit.Address, deposit.IsChange, deposit.AddressIndex, network, ct)
                    .ConfigureAwait(false);

                Utxo? confirmed = utxos.FirstOrDefault(u => u.IsConfirmed && u.BlockHeight.HasValue);

                if (confirmed != null)
                {
                    int tip = await _blockstreamClient.GetBlockHeightAsync(network, ct).ConfigureAwait(false);

                    confirmations = Math.Max(0, tip - confirmed.BlockHeight!.Value + 1);
                    deposit.TxId ??= confirmed.TxId;
                }    
            }

            DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            DepositStatus newStatus = DetermineStatus(deposit, stats, confirmations, utcNow);

            bool changed = !string.Equals(deposit.Status, newStatus.ToString(), StringComparison.Ordinal)
                        || deposit.ReceivedSats != stats.ConfirmedReceivedSats;

            if (changed)
            {
                deposit.Status = newStatus.ToString();
                deposit.ReceivedSats = stats.ConfirmedReceivedSats;

                if (newStatus == DepositStatus.Confirmed && deposit.ConfirmedUtc == null)
                {
                    deposit.ConfirmedUtc = utcNow;
                }

                await _transferRepository.UpdateDepositAsync(deposit, ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Deposit {DepositId} is now {Status} with {Received} sats and {Confirmations} confirmations.",
                    deposit.DepositId, newStatus, deposit.ReceivedSats, confirmations);
            }


            return deposit.ToResult(
                BuildPaymnetUri(deposit),
                stats.UnconfirmedReceivedSats,
                confirmations);
        }

        public async Task<IEnumerable<DepositResult>> GetDepositsAsync(string walletId, CancellationToken ct = default)
        {
            IEnumerable<DepositEntity> deposits = await _transferRepository
                .GetDepositsAsync(walletId, ct)
                .ConfigureAwait(false);

            List<DepositResult> result = new List<DepositResult>();
            foreach(DepositEntity deposit in deposits)
            {
                result.Add(deposit.ToResult(BuildPaymnetUri(deposit), 0, 0));
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

        private static string BuildPaymnetUri(DepositEntity deposit)
        {
            decimal? amountBtc = deposit.ExpectedSats.HasValue
                ? deposit.ExpectedSats.Value / 100_000_000m
                : null;

            return BipUriBuilder.Build(deposit.Address, amountBtc, deposit.Label);
        }

        private static DepositStatus DetermineStatus(
            DepositEntity deposit,
            AddressStats stats,
            int confirmations, 
            DateTime utcNow)
        {
            if (string.Equals(deposit.Status, DepositStatus.Confirmed.ToString(), StringComparison.Ordinal))
            {
                return DepositStatus.Confirmed;
            }

            if (stats.ConfirmedReceivedSats > 0 && confirmations >= RequiredConfirmations)
            {
                if (deposit.ExpectedSats.HasValue && stats.ConfirmedReceivedSats < deposit.ExpectedSats.Value)
                {
                    return DepositStatus.Underpaid;
                }

                return DepositStatus.Confirmed;
            }

            if (stats.TotalReceivedSats > 0)
            {
                return DepositStatus.Detected;
            }

            if (utcNow > deposit.ExpiresUtc)
            {
                return DepositStatus.Expired;
            }

            return DepositStatus.Pending;
        }
    }
}
