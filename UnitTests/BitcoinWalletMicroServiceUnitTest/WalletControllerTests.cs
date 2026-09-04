using BitcoinWalletMicroService.Controllers;
using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BitcoinWalletMicroServiceUnitTest
{
    public class WalletControllerTests
    {
        private readonly Mock<IWalletOrchestrator> _orchestrator = new Mock<IWalletOrchestrator>(MockBehavior.Strict);

        private WalletController CreateSut() => new WalletController(_orchestrator.Object);

        // ------------------------------------------------------------------- Create

        [Fact]
        public async Task GivenValidCredentials_WhenCallingCreate_ThenCreatesWalletWithWalletId()
        {
            var result = new CreateWalletResult
            {
                WalletId = TestData.WalletId,
                Label = "Test Wallet",
                Mnemonic = TestData.ValidMnemonic,
                Addresses = [TestData.KeyResult()]
            };

            _orchestrator
                .Setup(o => o.CreateWalletAsync(It.IsAny<CreateWalletModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            IActionResult response = await CreateSut().Create(ValidCreateDto(), CancellationToken.None);

            var created = Assert.IsType<CreatedResult>(response);
            Assert.Contains(TestData.WalletId, created.Location);
        }

        [Fact]
        public async Task GivenBadRequest_WhenCallingCreate_ThenReturnNullBody()
        {
            IActionResult response = await CreateSut().Create(null!, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(response);

            _orchestrator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GivenBadRequest_WhenCallingCreate_ThenOrchestratorRejectsInput()
        {
            _orchestrator
                .Setup(o => o.CreateWalletAsync(It.IsAny<CreateWalletModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Label is required"));

            IActionResult response = await CreateSut().Create(ValidCreateDto(), CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(response);

        }

        [Fact]
        public async Task GivenConflict_WhenCallingCreate_ThenWalletAlreadyExists()
        {
            _orchestrator
                .Setup(o => o.CreateWalletAsync(It.IsAny<CreateWalletModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("This wallet already exists."));

            IActionResult response = await CreateSut().Create(ValidCreateDto(), CancellationToken.None);

            var conflict = Assert.IsType<ConflictObjectResult>(response);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        }

        // ------------------------------------------------------------------- Import

        [Fact]
        public async Task GivenBadRequest_WhenCallingImport_ThenMnemonicFailsChecksum()
        {
            _orchestrator
                .Setup(o => o.ImportWalletAsync(It.IsAny<ImportWalletModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Mnemonic failed BIP39 checksum validation."));

            var dto = new ImportWalletDto
            {
                Label = "Imported",
                Mnemonic = "not a real mnemonic at all",
                Network = BitcoinWalletMicroService.Enums.BitcoinNetwork.TestNet,
                AddressType = BitcoinWalletMicroService.Enums.AddressType.NativeSegwit,
                InitalAddressCount = 1
            };

            IActionResult response = await CreateSut().Import(dto, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(response);
        }

        // ------------------------------------------------------------------- GetAddresses

        [Fact]
        public async Task GivenValidWallet_WhenCallingGetAddresses_ThenReturnNotEmptyCollection()
        {
            _orchestrator
                .Setup(o => o.GetWalletAsync(TestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.Summary());

            _orchestrator
                .Setup(o => o.GetAddressesAsync(TestData.WalletId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([TestData.KeyResult(0), TestData.KeyResult(1), TestData.KeyResult(2)]);

            IActionResult response = await CreateSut()
                .GetAddresses(TestData.WalletId, null, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var payload = Assert.IsAssignableFrom<IEnumerable<DerivedKeyDtoResponse>>(ok.Value);

            Assert.Equal(3, payload.Count());
        }

        [Fact]
        public async Task GivenWalletMissing_WhenCallingGetAddresses_ThenReturnsNotFound()
        {
            _orchestrator
                .Setup(o => o.GetWalletAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((WalletSummary)null!);

            IActionResult result = await CreateSut()
                .GetAddresses("missing-id", null, CancellationToken.None);

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GivenIsChangeFilter_WhenCallingGetAddresses_ThenPassesIsChangeFilterThrough(bool isChange)
        {
            _orchestrator
                .Setup(o => o.GetWalletAsync(TestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.Summary());

            _orchestrator
                .Setup(o => o.GetAddressesAsync(TestData.WalletId, isChange, It.IsAny<CancellationToken>()))
                .ReturnsAsync([TestData.KeyResult(0, isChange)]);

            await CreateSut().GetAddresses(TestData.WalletId, isChange, CancellationToken.None);

            _orchestrator.Verify(
                o => o.GetAddressesAsync(TestData.WalletId, isChange, It.IsAny<CancellationToken>()), Times.Once);

        }

        // ------------------------------------------------------------------- GetAll

        [Fact]
        public async Task GivenNotInternalModels_WhenCallingGetAll_ReturnsDto()
        {
            _orchestrator
                .Setup(o => o.GetWalletsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([TestData.Summary()]);

            IActionResult response = await CreateSut().GetAll(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);

            Assert.IsNotAssignableFrom<IEnumerable<WalletSummary>>(ok.Value);
            var payload = Assert.IsAssignableFrom<IEnumerable<WalletSummaryDtoResponse>>(ok.Value);
            Assert.Single(payload);
        }

        // ------------------------------------------------------------------- Delete

        [Fact]
        public async Task GivenDeleteWallet_WhenCallingDelete_ReturnsNoContent()
        {
            _orchestrator
                .Setup(o => o.DeleteWalletAsync(TestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            IActionResult response = await CreateSut().Delete(TestData.WalletId, CancellationToken.None);

            var status = Assert.IsType<StatusCodeResult>(response);
            Assert.Equal(StatusCodes.Status204NoContent, status.StatusCode);
        }

        [Fact]
        public async Task GivenNathingToDelete_WhenCallingDeleteWallet_ReturnsNotFound()
        {
            _orchestrator
                .Setup(o => o.DeleteWalletAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            IActionResult response = await CreateSut().Delete("missing-id", CancellationToken.None);

            Assert.IsType<NotFoundResult>(response);
        }

        private static CreateWalletDto ValidCreateDto() => new()
        {
            Label = "Test Wallet",
            Strength = BitcoinWalletMicroService.Enums.MnemonicStrength.Words12,
            Network = BitcoinWalletMicroService.Enums.BitcoinNetwork.TestNet,
            AddressType = BitcoinWalletMicroService.Enums.AddressType.NativeSegwit,
            InitialAddressCount = 1
        };

    }
}
