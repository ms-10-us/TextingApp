using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Orchestrator;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroService.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Entities;
using System.Data;

namespace BitcoinWalletMicroServiceUnitTest
{
    public class WalletOrchestratorTests
    {
        private readonly Mock<IWalletRepository> _repository = new Mock<IWalletRepository>();
        private readonly Mock<IMnemonicService> _mnemonicService = new Mock<IMnemonicService>();
        private readonly Mock<IKeyDerivationService> _keyDerivation = new Mock<IKeyDerivationService>();
        private readonly Mock<ISercretProtector> _secretProtector = new Mock<ISercretProtector>();

        private WalletOrchestrator CreateSut() => new WalletOrchestrator(
            _repository.Object,
            _mnemonicService.Object,
            _keyDerivation.Object,
            _secretProtector.Object);

        public WalletOrchestratorTests()
        {
            _mnemonicService
                .Setup(m => m.Generate(It.IsAny<MnemonicStrength>()))
                .Returns(new MnemonicResult
                {
                    Mnemonic = TestData.ValidMnemonic,
                    EntropyHex = "00000000000000000000000000000000",
                    WordCount = 12
                });

            _mnemonicService.Setup(m => m.IsValid(It.IsAny<string>())).Returns(true);
            _mnemonicService.Setup(m => m.ToSeed(It.IsAny<string>(), It.IsAny<string>())).Returns(new byte[64]);
            _mnemonicService.Setup(m => m.Fingerprint(It.IsAny<string>(), It.IsAny<string>())).Returns("fingerprint-abc");

            _secretProtector.Setup(s => s.Protect(It.IsAny<string>())).Returns([9, 9, 9]);

            _keyDerivation
                .Setup(k => k.DeriveAccount(It.IsAny<byte[]>(), It.IsAny<BitcoinNetwork>(),
                                            It.IsAny<AddressType>(), It.IsAny<int>()))
                .Returns(new AccountKeys
                {
                    AccountExtendedPublicKey = "vpub5Y...",
                    AccountDerivationPath = "m/84'/1'/0'"
                });

            _keyDerivation
                .Setup(k => k.DerivePublicKeys(It.IsAny<string>(), It.IsAny<BitcoinNetwork>(),
                                               It.IsAny<AddressType>(), It.IsAny<bool>(),
                                               It.IsAny<int>(), It.IsAny<int>()))
                .Returns((string _, BitcoinNetwork _, AddressType _, bool isChange, int start, int count) =>
                    Enumerable.Range(start, count).Select(i => TestData.KeyResult(i, isChange)).ToList());

            _repository
                .Setup(r => r.GetWalletByFingerprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WalletEntity?)null);


            _repository
                .Setup(r => r.InsertWalletAsync(It.IsAny<WalletEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.WalletId);

            _repository
                .Setup(r => r.InsertDerivedKeyAsync(It.IsAny<IEnumerable<DerivedKeyEntity>>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // ------------------------------------------------------------------- CreateWalletAsync

        [Fact]
        public async Task GivenWallet_WhenCallingCreateWalletAsync_PersistsWalletAndAddresses()
        {
            CreateWalletResult result = await CreateSut().CreateWalletAsync(Model(initialAddressCount: 3));

            Assert.Equal(3, result.Addresses.Count());
            Assert.Equal(TestData.ValidMnemonic, result.Mnemonic);

            _repository.Verify(r => r.InsertWalletAsync(
                It.IsAny<WalletEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);

            _repository.Verify(r => r.InsertDerivedKeyAsync(
                It.Is<IEnumerable<DerivedKeyEntity>>(k => k.Count() == 3),
                It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GivenMnemonicPhrase_WhenCallingCreateWalletAsync_EncryptsMnemonicBeforePersisting()
        {
            WalletEntity? persisted = null;

            _repository
                .Setup(r => r.InsertWalletAsync(It.IsAny<WalletEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<WalletEntity, IDbTransaction, CancellationToken>((w, _, _) => persisted = w)
                .ReturnsAsync(TestData.WalletId);

            await CreateSut().CreateWalletAsync(Model());

            _secretProtector.Verify(s => s.Protect(TestData.ValidMnemonic), Times.Once);

            Assert.NotNull(persisted);
            Assert.Equal(new byte[] { 9, 9, 9 }, persisted.EncryptedMnemonic);

            string asText = System.Text.Encoding.UTF8.GetString(persisted.EncryptedMnemonic);
            Assert.DoesNotContain("abandon", asText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GivenFingerprintExists_WhenCallingCreateWalletAsync_ThrowsConflict()
        {
            _repository
                .Setup(r => r.GetWalletByFingerprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.Wallet());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().CreateWalletAsync(Model()));

            _repository.Verify(r => r.InsertWalletAsync(
                It.IsAny<WalletEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GivenMissingLabel_WhenCallingCreateWalletAsync_ThrowsForMissingLabel(string? label)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateSut().CreateWalletAsync(Model(label: label!)));
        }

        [Fact]
        public async Task GivenLabelOver100Chars_WhenCallingCreateWalletAsync_ThrowsForLabelOver100Chars()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateSut().CreateWalletAsync(Model(label: new string('x', 101))));

        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(3, 3)]
        [InlineData(500, 100)]
        public async Task GivenAddress_WhenCallingCreateWalletAsync_ClampsAddressCount(int requested, int expected)
        {
            CreateWalletResult result = await CreateSut().CreateWalletAsync(Model(initialAddressCount: requested));

            Assert.Equal(expected, result.Addresses.Count());
        }

        [Fact]
        public async Task GivenWallet_WhenCallingCreateWalletAsync_ThenDerivesReceiveChainOnly()
        {
            await CreateSut().CreateWalletAsync(Model(initialAddressCount: 2));

            _keyDerivation.Verify(k => k.DerivePublicKeys(It.IsAny<string>(), It.IsAny<BitcoinNetwork>(),
                It.IsAny<AddressType>(), false, 0, 2), Times.Once);
        }

        // ------------------------------------------------------------------- ImportWalletAsync

        [Fact]
        public async Task GivenInvalidChecksum_WhenCallingImportWalletAsync_ThrowsExcpetion()
        {
            _mnemonicService.Setup(m => m.IsValid(It.IsAny<string>())).Returns(false);

            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateSut().ImportWalletAsync(ImportModel()));

            _repository.Verify(r => r.InsertWalletAsync(
                It.IsAny<WalletEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GivenImportWallet_WhenCallingImportWalletAsync_UsesSuppliedMnemonic()
        {
            await CreateSut().ImportWalletAsync(ImportModel());

            _mnemonicService.Verify(m => m.Generate(It.IsAny<MnemonicStrength>()), Times.Never);
            _mnemonicService.Verify(m => m.ToSeed(TestData.ValidMnemonic, It.IsAny<string>()), Times.Once);
        }

        // ------------------------------------------------------------------- GetAddressesAsync

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressesAsync_ReturnsEveryKey()
        {
            _repository
                .Setup(r => r.GetDerivedKeysAsync(TestData.WalletId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([TestData.KeyEntity(0), TestData.KeyEntity(1), TestData.KeyEntity(2)]);

            IEnumerable<DerivedKeyResult> result = await CreateSut()
                .GetAddressesAsync(TestData.WalletId, null);

            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressesAsync_MapsFielsFaithfully()
        {
            DerivedKeyEntity entity = TestData.KeyEntity(5, isChange: true);

            _repository.Setup(r => r.GetDerivedKeysAsync(TestData.WalletId, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync([entity]);

            DerivedKeyResult mapped = (await CreateSut().GetAddressesAsync(TestData.WalletId, true)).Single();

            Assert.Equal(entity.Address, mapped.Address);
            Assert.Equal(entity.AddressIndex, mapped.AddressIndex);
            Assert.Equal(entity.DerivationPath, mapped.DerivationPath);
            Assert.True(mapped.IsChange);

            Assert.Null(mapped.PrivateKeyWif);
        }

        // ------------------------------------------------------------------- GetWalletsAsync

        [Fact]
        public async Task GivenWallet_WhenCallingGetWalletsAsync_ReturnsEverySummary()
        {

            _repository
                .Setup(r => r.GetWalletSummariesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new WalletSummaryEntity {WalletId = "a", Label = "A"},
                    new WalletSummaryEntity {WalletId = "b", Label = "B"}
                    ]);

            IEnumerable<WalletSummary> result = await CreateSut().GetWalletsAsync();

            Assert.Equal(2, result.Count());
        }

        // ------------------------------------------------------------------- GetWalletByIdAsync

        [Fact]
        public async Task GivenWalletNotFound_WhenCallingGetWalletAsync_ReturnNull()
        {
            _repository
                .Setup(r => r.GetWalletByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WalletEntity)null!);

            Assert.Null(await CreateSut().GetWalletAsync("missing-id"));
        }

        [Fact]
        public async Task GivenWallet_WhenCallingGetWalletAsync_CountAddresses()
        {
            _repository
                .Setup(r => r.GetWalletByIdAsync(TestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.Wallet());

            _repository
                .Setup(r => r.GetDerivedKeysAsync(TestData.WalletId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([TestData.KeyEntity(0), TestData.KeyEntity(1)]);

            WalletSummary summary = await CreateSut().GetWalletAsync(TestData.WalletId);

            Assert.Equal(2, summary.AddressCount);
            Assert.Equal("TestNet", summary.Network);
        }

        [Fact]
        public void Constructor_ThrowsWhenAnyDependencyIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new WalletOrchestrator(
                null!, _mnemonicService.Object, _keyDerivation.Object, _secretProtector.Object));

            Assert.Throws<ArgumentNullException>(() => new WalletOrchestrator(
                _repository.Object, null!, _keyDerivation.Object, _secretProtector.Object));

            Assert.Throws<ArgumentNullException>(() => new WalletOrchestrator(
                _repository.Object, _mnemonicService.Object, null!, _secretProtector.Object));

            Assert.Throws<ArgumentNullException>(() => new WalletOrchestrator(
                _repository.Object, _mnemonicService.Object, _keyDerivation.Object, null!));
        }








        private static CreateWalletModel Model(
            string label = "Test Wallet",
            int initialAddressCount = 1) => new()
            {
                Label = label,
                Strength = MnemonicStrength.Words12,
                Network = BitcoinNetwork.TestNet,
                AddressType = AddressType.NativeSegwit,
                InitialAddressCount = initialAddressCount
            };

        private static ImportWalletModel ImportModel() => new()
        {
            Label = "Imported",
            Mnemonic = TestData.ValidMnemonic,
            Network = BitcoinNetwork.TestNet,
            AddressType = AddressType.NativeSegwit,
            InitalAddressCount = 1
        };
    }
}
