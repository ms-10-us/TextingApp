using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Mappings;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroService.Utilities;
using System.Collections;

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

        public Task<CreateWalletResult> ImportWalletAsync(
            ImportWalletModel model, 
            CancellationToken ct = default(CancellationToken))
        {
            if (model == null)
            {
                throw new ArgumentNullException("request");
            }
            ValidateLabel(model.Label);

            if (string.IsNullOrWhiteSpace(model.Mnemonic))
            {
                throw new ArgumentException("Mnemonic is required.", "request");
            }

            if (!_mnemonicService.IsValid(model.Mnemonic))
            {
                throw new ArgumentException("Mnemonic failed BIP39 checksum validation.", "request");
            }

            return PersistWalletAsync(
                model.Label,
                model.Mnemonic,
                model.Passphrase,
                model.Network,
                model.AddressType,
                model.InitalAddressCount,
                ct);
        }

        public async Task<IEnumerable<WalletSummary>> GetWalletsAsync(CancellationToken ct = default(CancellationToken))
        {   
            IEnumerable<WalletSummaryEntity> entity = await _repository.GetWalletSummariesAsync(ct).ConfigureAwait(false);
            List<WalletSummary> result = new List<WalletSummary>();
            foreach(WalletSummaryEntity entityItem in entity)
            {
                result.Add(entityItem.ToModel());
            }

            return result;
        }

        public async Task<WalletSummary> GetWalletAsync(string walletId, CancellationToken ct = default(CancellationToken))
        {

            WalletEntity wallet = await _repository.GetWalletByIdAsync(walletId).ConfigureAwait(false);
            if (wallet == null)
            {
                return null;
            }

            IEnumerable<DerivedKeyEntity> keys = await _repository.GetDerivedKeysAsync(walletId, null, ct).ConfigureAwait(false);

            return new WalletSummary
            {
                WalletId = wallet.Id,
                Label = wallet.Label,
                Network = wallet.Network,
                AccountExtendedPublicKey = wallet.AccountExtendedPublicKey,
                AccountDerivationPath = wallet.AccountDerivationPath,
                AddressCount = keys.Count(),
                CreatedUtc = wallet.CreatedUtc
            };
        }

        public async Task<IEnumerable<DerivedKeyResult>> GetAddressesAsync(
            string walletId,
            bool? isChange = null,
            CancellationToken ct = default(CancellationToken))
        {
            IEnumerable<DerivedKeyEntity> keys = await _repository.GetDerivedKeysAsync(walletId, isChange, ct).ConfigureAwait(false);
            keys.Select(k => new DerivedKeyEntity
            {
                WalletId = walletId,
                AddressIndex = k.AddressIndex,
                IsChange = k.IsChange == true,
                DerivationPath = k.DerivationPath,
                PublicKeyHex = k.PublicKeyHex,
                Address = k.Address,
            }).ToList();

            List<DerivedKeyResult> result = new List<DerivedKeyResult>();
            foreach(DerivedKeyEntity key in keys)
            {
                result.Add(key.ToModel());
            }

            return result;
        }

        public async Task<bool> DeleteWalletAsync(string walletId, CancellationToken ct)
        {
            return await _repository.DeleteWalletAsync(walletId, ct).ConfigureAwait(false);
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

            WalletEntity? existing = await _repository
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

                IEnumerable<DerivedKeyResult> addresses = _keyDerivationService.DerivePublicKeys(
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
