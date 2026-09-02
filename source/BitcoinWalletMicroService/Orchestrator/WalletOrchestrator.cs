using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Mappings;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroService.Utilities;

namespace BitcoinWalletMicroService.Orchestrator
{
    public class WalletOrchestrator : IWalletOrchestrator
    {
        private const int MaxInitialAddresses = 100;

        private readonly IWalletRepository _repository;
        private readonly IMnemonicService _mnemonicService;
        private readonly IKeyDerivationService _keyDerivationService;
        private readonly ISercretProtector _sercretProtector;

        public WalletOrchestrator(
            IWalletRepository repository,
            IMnemonicService mnemonicService,
            IKeyDerivationService keyDerivationService,
            ISercretProtector sercretProtector)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            if (mnemonicService == null)
            {
                throw new ArgumentNullException("mnemonicService");
            }

            if (keyDerivationService == null)
            {
                throw new ArgumentNullException("keyDerivationService");
            }

            if (sercretProtector == null)
            {
                throw new ArgumentNullException("secretProtector");
            }

            _repository = repository;
            _mnemonicService = mnemonicService;
            _keyDerivationService = keyDerivationService;
            _sercretProtector = sercretProtector;
        }

        public Task<CreateWalletResult> CreateWalletAsync(CreateWalletModel model, CancellationToken ct = default)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }
            ValidateLabel(model.Label);

            MnemonicResult generated = _mnemonicService.Generate(model.Strength);

            return PersistWalletAsync(
                model.Label,
                generated.Mnemonic,
                model.Passphrase,
                model.Network,
                model.AddressType,
                model.InitialAddressCount,
                ct);
        }

        private static void ValidateLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Label is required");
            }

            if (label.Trim().Length > 100)
            {
                throw new ArgumentException("Label must be 100 characters or less.");
            }
        }

        private async Task<CreateWalletResult> PersistWalletAsync(
            string lablel,
            string mnemonic,
            string passphrase,
            BitcoinNetwork network,
            AddressType addressType,
            int initialAddressCount,
            CancellationToken cancellationToken)
        {
            int addressCount = Math.Max(1, Math.Min(initialAddressCount, MaxInitialAddresses));

            string fingerprint = _mnemonicService.Fingerprint(mnemonic, passphrase);

            //GetWalletByFingerprintAsync from repository;
            WalletEntity existing = await _repository
                .GetWalletByFingerprintAsync(fingerprint, cancellationToken)
                .ConfigureAwait(false);

            if (existing != null)
            {
                throw new InvalidOperationException("This wallet already exists (id " + existing.Id + ").");
            }

            byte[] seed = null;
            try
            {
                seed = _mnemonicService.ToSeed(mnemonic, passphrase);

                AccountKeys account = _keyDerivationService.DeriveAccount(seed, network, addressType, 0);

                IReadOnlyList<DerivedKeyResult> addresses = _keyDerivationService.DerivePublicKeys(
                    account.AccountExtendedPublicKey, network, addressType, false, 0, addressCount);

                var walletId = Guid.NewGuid().ToString("D");
                DateTime now = DateTime.UtcNow;

                WalletEntity entity = new WalletEntity
                {
                    Id = walletId,
                    Label = lablel.Trim(),
                    Network = network.ToString(),
                    EncryptedMnemonic = _sercretProtector.Protect(mnemonic),
                    MnemonicFingerprint = fingerprint,
                    AccountExtendedPublicKey = account.AccountExtendedPublicKey,
                    AccountDerivationPath = account.AccountDerivationPath,
                    CreatedUtc = now
                };

                List<DerivedKeyEntity> keyEntities = addresses.Select(a => a.ToEntity(walletId, now))
                    .ToList();

                await _repository.InsertWalletAsync(entity, null, cancellationToken).ConfigureAwait(false);
                await _repository.InsertDerivedKeyAsync(keyEntities, null, cancellationToken).ConfigureAwait(false);

                return new CreateWalletResult
                {
                    WalletId = walletId,
                    Label = entity.Label,
                    Mnemonic = mnemonic,
                    AccountExtendedPublicKey = account.AccountExtendedPublicKey,
                    AccountDerivationPath = account.AccountDerivationPath,
                    Addresses = addresses,
                    CreatedUtc = now
                };

            }
            finally
            {
                if (seed != null)
                {
                    Array.Clear(seed, 0, seed.Length);
                }
            }

        }

    }
}
