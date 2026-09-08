using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroService.Utilities;
using BitcoinWalletMicroServiceUnitTest.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Data;

namespace BitcoinWalletMicroServiceUnitTest
{
    public class WalletTransferOrchestratorTests
    {
        private readonly Mock<IWalletRepository> _repository = new();
        private readonly Mock<IWalletTransferRepository> _transferRepository = new();
        private readonly Mock<IMnemonicService> _mnemonicService = new();
        private readonly Mock<IKeyDerivationService> _keyDerivation = new();
        private readonly Mock<ISercretProtector> _secretProtector = new();
        private readonly Mock<IBlockstreamClient> _blockstream = new();
        private readonly Mock<ITransactionBuilderService> _transactionBuilder = new();
        private readonly Mock<ILogger<WalletTransferOrchestrator>> _logger = new();
        private readonly FakeTimeProvider _timeProvider = new(TransferTestData.Now);

        private WalletTransferOrchestrator CreateSut() => new(
            _repository.Object,
            _transferRepository.Object,
            _mnemonicService.Object,
            _keyDerivation.Object,
            _secretProtector.Object,
            _blockstream.Object,
            _transactionBuilder.Object,
            _logger.Object,
            _timeProvider);

        public WalletTransferOrchestratorTests()
        {
            _repository
                .Setup(r => r.GetWalletByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.Wallet());

            _repository
                .Setup(r => r.GetNextAddressIndexAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            _repository
                .Setup(r => r.GetDerivedKeysAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([TransferTestData.KeyEntity()]);

            _repository
                .Setup(r => r.InsertDerivedKeyAsync(It.IsAny<IEnumerable<DerivedKeyEntity>>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _keyDerivation
                .Setup(k => k.DerivePublicKeys(It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<AddressType>(),
                                               It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((string _, BitcoinNetwork _, AddressType _, bool isChange, int start, int count) =>
                    Enumerable.Range(start, count).Select(i => TransferTestData.KeyResult(i, isChange)).ToList());

            _keyDerivation
                .Setup(k => k.DerivePrivateKeys(It.IsAny<byte[]>(), It.IsAny<BitcoinNetwork>(), It.IsAny<AddressType>(),
                                                It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>()))
                .Returns(TransferTestData.KeyResult());

            _keyDerivation
                .Setup(k => k.DeriveAccount(It.IsAny<byte[]>(), It.IsAny<BitcoinNetwork>(), It.IsAny<AddressType>(), It.IsAny<int>()))
                .Returns(new AccountKeys
                {
                    AccountExtendedPublicKey = TransferTestData.AccountXpub,
                    AccountDerivationPath = "m/84'/1'/0'"
                });

            _secretProtector.Setup(s => s.Unprotect(It.IsAny<byte[]>())).Returns("abandon abandon about");
            _mnemonicService.Setup(m => m.ToSeed(It.IsAny<string>(), It.IsAny<string>())).Returns(new byte[64]);

            _blockstream
                .Setup(b => b.GetUtxoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(),
                                           It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([TransferTestData.Utxo()]);

            _blockstream
                .Setup(b => b.GetFeeRateAsync(It.IsAny<BitcoinNetwork>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5m);

            _blockstream
                .Setup(b => b.GetAddressStatsAsync(It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.Stats());

            _blockstream
                .Setup(b => b.GetBlockHeightAsync(It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(2_500_005);

            _blockstream
                .Setup(b => b.BroadcastAsync(It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("f0e1d2c3b4a5968778695a4b3c2d1e0ff0e1d2c3b4a5968778695a4b3c2d1e0f");

            _transactionBuilder
                .Setup(t => t.Build(It.IsAny<IEnumerable<Utxo>>(), It.IsAny<IDictionary<string, string>>(),
                                    It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(),
                                    It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<BitcoinNetwork>()))
                .Returns(TransferTestData.Built());

            _transferRepository
                .Setup(r => r.GetSendTransactionByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SendTransactionEntity?)null);

            _transferRepository
                .Setup(r => r.InsertSentTransactionAsync(It.IsAny<SendTransactionEntity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _transferRepository
                .Setup(r => r.InsertDepositAsync(It.IsAny<DepositEntity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _transferRepository
                .Setup(r => r.UpdateDepositAsync(It.IsAny<DepositEntity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // -------------------------------------------------------------------
        // GetNextReceiveAddressAsync
        // -------------------------------------------------------------------

        [Fact]
        public async Task GetNextReceiveAddress_PersistsTheDerivedAddress()
        {
            await CreateSut().GetNextReceiveAddressAsync(TransferTestData.WalletId);

            _repository.Verify(r => r.InsertDerivedKeyAsync(
                It.Is<IEnumerable<DerivedKeyEntity>>(k => k.Count() == 1),
                It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetNextReceiveAddress_UsesTheNextFreeIndex()
        {
            _repository
                .Setup(r => r.GetNextAddressIndexAsync(TransferTestData.WalletId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(7);

            ReceiveAddressResult result = await CreateSut()
                .GetNextReceiveAddressAsync(TransferTestData.WalletId);

            Assert.Equal(7, result.AddressIndex);
        }

        [Fact]
        public async Task GetNextReceiveAddress_ThrowsWhenWalletMissing()
        {
            _repository
                .Setup(r => r.GetWalletByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WalletEntity?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => CreateSut().GetNextReceiveAddressAsync("missing"));
        }

        [Fact]
        public async Task GetNextReceiveAddress_NeverDecryptsTheMnemonic()
        {
            await CreateSut().GetNextReceiveAddressAsync(TransferTestData.WalletId);

            _secretProtector.Verify(s => s.Unprotect(It.IsAny<byte[]>()), Times.Never);
            _mnemonicService.Verify(m => m.ToSeed(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // -------------------------------------------------------------------
        // GetBalanceAsync
        // -------------------------------------------------------------------

        [Fact]
        public async Task GetBalance_SeparatesConfirmedFromUnconfirmed()
        {
            _blockstream
                .Setup(b => b.GetUtxoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(),
                                           It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    TransferTestData.Utxo(valueSats: 100_000, isConfirmed: true),
                    TransferTestData.Utxo(valueSats: 25_000, isConfirmed: false)
                ]);

            WalletBalance balance = await CreateSut().GetBalanceAsync(TransferTestData.WalletId);

            Assert.Equal(100_000, balance.ConfirmedSats);
            Assert.Equal(25_000, balance.UncofirmedSats);
            Assert.Equal(125_000, balance.TotalSats);
            Assert.Equal(2, balance.UtxoCount);
        }

        [Fact]
        public async Task GetBalance_ReturnsZero_WhenNoUtxos()
        {
            _blockstream
                .Setup(b => b.GetUtxoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(),
                                           It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            WalletBalance balance = await CreateSut().GetBalanceAsync(TransferTestData.WalletId);

            Assert.Equal(0, balance.TotalSats);
            Assert.Equal(0, balance.UtxoCount);
        }

        // -------------------------------------------------------------------
        // SendAsync - validation
        // -------------------------------------------------------------------

        [Fact]
        public async Task SendAsync_ThrowsForNullModel()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().SendAsync(null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendAsync_ThrowsForMissingDestination(string? toAddress)
        {
            SendModel model = TransferTestData.SendModel();
            model.ToAddress = toAddress!;

            await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().SendAsync(model));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task SendAsync_ThrowsForNonPositiveAmount(long amountSats)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateSut().SendAsync(TransferTestData.SendModel(amountSats: amountSats)));
        }

        [Fact]
        public async Task SendAsync_AllowsZeroAmount_WhenSweepingAll()
        {
            SendModel model = TransferTestData.SendModel(amountSats: 0, sweepAll: true);

            SendResult result = await CreateSut().SendAsync(model);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task SendAsync_ThrowsWhenBroadcastingWithoutIdempotencyKey()
        {
            SendModel model = TransferTestData.SendModel(dryRun: false, idempotencyKey: null);

            await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().SendAsync(model));
        }

        // -------------------------------------------------------------------
        // SendAsync - dry run
        // -------------------------------------------------------------------

        [Fact]
        public async Task SendAsync_DryRun_DoesNotBroadcast()
        {
            SendResult result = await CreateSut().SendAsync(TransferTestData.SendModel(dryRun: true));

            Assert.Null(result.TxId);
            Assert.False(result.Broadcast);

            _blockstream.Verify(b => b.BroadcastAsync(
                It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()), Times.Never);

            _transferRepository.Verify(r => r.InsertSentTransactionAsync(
                It.IsAny<SendTransactionEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_DryRun_StillReturnsSignedHexAndFee()
        {
            SendResult result = await CreateSut().SendAsync(TransferTestData.SendModel(dryRun: true));

            Assert.NotEmpty(result.RawTransactionHex);
            Assert.Equal(250, result.FeeSats);
            Assert.Equal(141, result.VirtualSizeBytes);
        }

        [Fact]
        public async Task SendAsync_DryRun_SendsChangeToAFreshAddress()
        {
            await CreateSut().SendAsync(TransferTestData.SendModel(dryRun: true));

            _repository.Verify(r => r.GetNextAddressIndexAsync(
                TransferTestData.WalletId, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_ThrowsUnauthorized_WhenPassphraseDoesNotMatch()
        {
            _keyDerivation
                .Setup(k => k.DeriveAccount(It.IsAny<byte[]>(), It.IsAny<BitcoinNetwork>(), It.IsAny<AddressType>(), It.IsAny<int>()))
                .Returns(new AccountKeys
                {
                    AccountExtendedPublicKey = "vpubDIFFERENTKEYENTIRELY",
                    AccountDerivationPath = "m/84'/1'/0'"
                });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().SendAsync(TransferTestData.SendModel(dryRun: true)));
        }

        [Fact]
        public async Task SendAsync_Broadcasts_WhenNoPriorTransactionExists()
        {
            SendModel model = TransferTestData.SendModel(
                dryRun: false, idempotencyKey: TransferTestData.IdempotencyKey);

            SendResult result = await CreateSut().SendAsync(model);

            Assert.True(result.Broadcast);
            Assert.NotNull(result.TxId);
            Assert.False(result.WasIdempotentReplay);

            _blockstream.Verify(b => b.BroadcastAsync(
                It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_RecordsTheTransaction_AfterBroadcast()
        {
            SendModel model = TransferTestData.SendModel(
                dryRun: false, idempotencyKey: TransferTestData.IdempotencyKey);

            await CreateSut().SendAsync(model);

            _transferRepository.Verify(r => r.InsertSentTransactionAsync(
                It.Is<SendTransactionEntity>(e =>
                    e.IdempotencyKey == TransferTestData.IdempotencyKey &&
                    e.WalletId == TransferTestData.WalletId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendAsync_StillReturnsTxId_WhenRecordingFails()
        {
            _transferRepository
                .Setup(r => r.InsertSentTransactionAsync(It.IsAny<SendTransactionEntity>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database is locked"));

            SendModel model = TransferTestData.SendModel(
                dryRun: false, idempotencyKey: TransferTestData.IdempotencyKey);

            SendResult result = await CreateSut().SendAsync(model);

            Assert.NotNull(result.TxId);
            Assert.True(result.Broadcast);
        }

        [Fact]
        public async Task SendAsync_ReturnsOriginal_OnIdempotentReplay()
        {
            _transferRepository
                .Setup(r => r.GetSendTransactionByIdempotencyKeyAsync(
                    TransferTestData.WalletId, TransferTestData.IdempotencyKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.SendEntity());

            SendModel model = TransferTestData.SendModel(
                dryRun: false, idempotencyKey: TransferTestData.IdempotencyKey);

            SendResult result = await CreateSut().SendAsync(model);

            Assert.True(result.WasIdempotentReplay);
            Assert.Equal(TransferTestData.SendEntity().TxId, result.TxId);

            _blockstream.Verify(b => b.BroadcastAsync(
                It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()), Times.Never);

            _transactionBuilder.Verify(t => t.Build(
                It.IsAny<IEnumerable<Utxo>>(), It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<BitcoinNetwork>()), Times.Never);
        }

        // -------------------------------------------------------------------
        // CreateDepositeAsync
        // -------------------------------------------------------------------

        [Fact]
        public async Task CreateDeposit_ReturnsPendingWithFreshAddress()
        {
            DepositResult result = await CreateSut().CreateDepositeAsync(TransferTestData.DepositModel());

            Assert.Equal(DepositStatus.Pending, result.Status);
            Assert.Equal(0, result.ReceivedSats);
            Assert.NotEmpty(result.Address);
            Assert.NotEmpty(result.DepositId);
        }

        [Fact]
        public async Task CreateDeposit_PersistsBothTheAddressAndTheDeposit()
        {
            await CreateSut().CreateDepositeAsync(TransferTestData.DepositModel());

            _repository.Verify(r => r.InsertDerivedKeyAsync(
                It.IsAny<IEnumerable<DerivedKeyEntity>>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _transferRepository.Verify(r => r.InsertDepositAsync(
                It.IsAny<DepositEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateDeposit_EmbedsAmountInThePaymentUri()
        {
            DepositResult result = await CreateSut()
                .CreateDepositeAsync(TransferTestData.DepositModel(expectedSats: 50_000));

            Assert.Contains("amount=0.0005", result.PaymentUri);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(293)]
        public async Task CreateDeposit_ThrowsForAmountBelowDust(long expectedSats)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateSut().CreateDepositeAsync(TransferTestData.DepositModel(expectedSats)));
        }

        [Fact]
        public async Task CreateDeposit_AllowsNoExpectedAmount()
        {
            DepositResult result = await CreateSut()
                .CreateDepositeAsync(TransferTestData.DepositModel(expectedSats: null));

            Assert.Null(result.ExpectedSats);
        }

        [Fact]
        public async Task CreateDeposit_ThrowsWhenWalletMissing()
        {
            _repository
                .Setup(r => r.GetWalletByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WalletEntity?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => CreateSut().CreateDepositeAsync(TransferTestData.DepositModel()));
        }

        [Fact]
        public async Task GetDeposit_QueriesTheDepositAddress_NotTheWalletId()
        {
            _transferRepository
                .Setup(r => r.GetDepositByIdAsync(TransferTestData.WalletId, TransferTestData.DepositId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.DepositEntity());

            await CreateSut().GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            _blockstream.Verify(b => b.GetAddressStatsAsync(
                TransferTestData.Address, It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()), Times.Once);

            _blockstream.Verify(b => b.GetAddressStatsAsync(
                TransferTestData.WalletId, It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetDeposit_ThrowsWhenUnknown()
        {
            _transferRepository
                .Setup(r => r.GetDepositByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DepositEntity?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => CreateSut().GetDepositAsync(TransferTestData.WalletId, "missing"));
        }

        [Fact]
        public async Task GetDeposit_StaysPending_WhenNothingReceived()
        {
            SetupDeposit(TransferTestData.DepositEntity());
            SetupStats(confirmed: 0, unconfirmed: 0);

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Pending, result.Status);
        }

        [Fact]
        public async Task GetDeposit_IsDetected_WhenOnlyInMempool()
        {
            SetupDeposit(TransferTestData.DepositEntity());
            SetupStats(confirmed: 0, unconfirmed: 50_000);

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Detected, result.Status);
            Assert.Equal(50_000, result.UnconfirmedSats);
        }

        [Fact]
        public async Task GetDeposit_IsDetected_WhenConfirmationsBelowThreshold()
        {
            SetupDeposit(TransferTestData.DepositEntity());
            SetupStats(confirmed: 50_000);
            SetupConfirmations(blockHeight: 2_500_000, tip: 2_500_002);   // 3 confirmations

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Detected, result.Status);
            Assert.Equal(3, result.Confirmations);
        }

        [Fact]
        public async Task GetDeposit_IsConfirmed_AtSixConfirmations()
        {
            SetupDeposit(TransferTestData.DepositEntity());
            SetupStats(confirmed: 50_000);
            SetupConfirmations(blockHeight: 2_500_000, tip: 2_500_005);   // 6 confirmations

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Confirmed, result.Status);
            Assert.Equal(6, result.Confirmations);
            Assert.Equal(50_000, result.ReceivedSats);
        }

        [Fact]
        public async Task GetDeposit_IsUnderpaid_WhenLessThanExpectedArrives()
        {
            SetupDeposit(TransferTestData.DepositEntity(expectedSats: 50_000));
            SetupStats(confirmed: 30_000);
            SetupConfirmations(blockHeight: 2_500_000, tip: 2_500_005);

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Underpaid, result.Status);
            Assert.Equal(30_000, result.ReceivedSats);
        }

        [Fact]
        public async Task GetDeposit_IsExpired_WhenWindowElapsedWithNoPayment()
        {
            SetupDeposit(TransferTestData.DepositEntity(
                expiresUtc: TransferTestData.Now.AddMinutes(-1)));

            SetupStats(confirmed: 0, unconfirmed: 0);

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Expired, result.Status);
        }

        [Fact]
        public async Task GetDeposit_ConfirmedIsTerminal_EvenAfterExpiry()
        {
            SetupDeposit(TransferTestData.DepositEntity(
                status: "Confirmed", expiresUtc: DateTime.UtcNow.AddMinutes(-1)));
            SetupStats(confirmed: 50_000);

            DepositResult result = await CreateSut()
                .GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            Assert.Equal(DepositStatus.Confirmed, result.Status);
        }

        [Fact]
        public async Task GetDeposit_PersistsAStatusChange()
        {
            SetupDeposit(TransferTestData.DepositEntity());
            SetupStats(confirmed: 50_000);
            SetupConfirmations(blockHeight: 2_500_000, tip: 2_500_005);

            await CreateSut().GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            _transferRepository.Verify(r => r.UpdateDepositAsync(
                It.Is<DepositEntity>(d => d.Status == "Confirmed" && d.ConfirmedUtc != null),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetDeposit_DoesNotWrite_WhenNothingChanged()
        {
            SetupDeposit(TransferTestData.DepositEntity());
            SetupStats(confirmed: 0, unconfirmed: 0);

            await CreateSut().GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId);

            _transferRepository.Verify(r => r.UpdateDepositAsync(
                It.IsAny<DepositEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // -------------------------------------------------------------------
        // Lists
        // -------------------------------------------------------------------

        [Fact]
        public async Task GetDeposits_ReturnsEveryRow()
        {
            _transferRepository
                .Setup(r => r.GetDepositsAsync(TransferTestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    TransferTestData.DepositEntity(),
                    TransferTestData.DepositEntity(status: "Confirmed"),
                    TransferTestData.DepositEntity(status: "Expired")
                ]);

            IEnumerable<DepositResult> result = await CreateSut()
                .GetDepositsAsync(TransferTestData.WalletId);

            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task GetDeposits_DoesNotCallTheExplorer()
        {
            _transferRepository
                .Setup(r => r.GetDepositsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([TransferTestData.DepositEntity()]);

            await CreateSut().GetDepositsAsync(TransferTestData.WalletId);

            _blockstream.Verify(b => b.GetAddressStatsAsync(
                It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetSendTransactions_ReturnsEveryRow()
        {
            _transferRepository
                .Setup(r => r.GetSendTransactionAsync(TransferTestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([TransferTestData.SendEntity(), TransferTestData.SendEntity()]);

            IEnumerable<SendTransaction> result = await CreateSut()
                .GetSendTransactionAsync(TransferTestData.WalletId, CancellationToken.None);

            Assert.Equal(2, result.Count());
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void SetupDeposit(DepositEntity deposit)
        {
            _transferRepository
                .Setup(r => r.GetDepositByIdAsync(TransferTestData.WalletId, TransferTestData.DepositId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deposit);
        }

        private void SetupStats(long confirmed = 0, long unconfirmed = 0)
        {
            _blockstream
                .Setup(b => b.GetAddressStatsAsync(It.IsAny<string>(), It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.Stats(confirmed, unconfirmed));
        }

        private void SetupConfirmations(int blockHeight, int tip)
        {
            _blockstream
                .Setup(b => b.GetUtxoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(),
                                           It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([TransferTestData.Utxo(isConfirmed: true, blockHeight: blockHeight)]);

            _blockstream
                .Setup(b => b.GetBlockHeightAsync(It.IsAny<BitcoinNetwork>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tip);
        }
    }
}