using BitcoinWalletMicroService.Controllers;
using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using BitcoinWalletMicroServiceUnitTest.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BitcoinWalletMicroServiceUnitTest
{
    public class WalletTransferControllerTests
    {
        private readonly Mock<IWalletTransferOrchestrator> _orchestrator = new();

        private WalletTransferController CreateSut() => new(_orchestrator.Object);

        // -------------------------------------------------------------------
        // GetReceiveAddress
        // -------------------------------------------------------------------

        [Fact]
        public async Task GetReceiveAddress_ReturnsOk_WithAddressAndPaymentUri()
        {
            _orchestrator
                .Setup(o => o.GetNextReceiveAddressAsync(TransferTestData.WalletId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.ReceiveAddress());

            IActionResult response = await CreateSut()
                .GetReceiveAddress(TransferTestData.WalletId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var dto = Assert.IsType<ReceiveAddressDtoResponse>(ok.Value);

            Assert.Equal(TransferTestData.Address, dto.Address);
            Assert.NotEmpty(dto.PaymentUri);
        }

        [Fact]
        public async Task GetReceiveAddress_RequestsTheReceiveChain_NotChange()
        {
            _orchestrator
                .Setup(o => o.GetNextReceiveAddressAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.ReceiveAddress());

            await CreateSut().GetReceiveAddress(TransferTestData.WalletId, CancellationToken.None);

            _orchestrator.Verify(
                o => o.GetNextReceiveAddressAsync(TransferTestData.WalletId, false, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetReceiveAddress_ReturnsNotFound_WhenWalletMissing()
        {
            _orchestrator
                .Setup(o => o.GetNextReceiveAddressAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            IActionResult response = await CreateSut().GetReceiveAddress("missing", CancellationToken.None);

            Assert.IsType<NotFoundResult>(response);
        }

        // -------------------------------------------------------------------
        // GetBalance
        // -------------------------------------------------------------------

        [Fact]
        public async Task GetBalance_ReturnsOk_WithConfirmedAndUnconfirmedSplit()
        {
            _orchestrator
                .Setup(o => o.GetBalanceAsync(TransferTestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.Balance(confirmed: 100_000, unconfirmed: 25_000));

            IActionResult response = await CreateSut()
                .GetBalance(TransferTestData.WalletId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var dto = Assert.IsType<WalletBalanceDtoResponse>(ok.Value);

            Assert.Equal(100_000, dto.ConfirmedSats);
            Assert.Equal(125_000, dto.TotalSats);
        }

        [Fact]
        public async Task GetBalance_ReturnsNotFound_WhenWalletMissing()
        {
            _orchestrator
                .Setup(o => o.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            Assert.IsType<NotFoundResult>(
                await CreateSut().GetBalance("missing", CancellationToken.None));
        }

        [Fact]
        public async Task GetBalance_Returns502_WhenExplorerUnreachable()
        {
            _orchestrator
                .Setup(o => o.GetBalanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("connection refused"));

            IActionResult response = await CreateSut()
                .GetBalance(TransferTestData.WalletId, CancellationToken.None);

            var status = Assert.IsType<ObjectResult>(response);
            Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        }

        // -------------------------------------------------------------------
        // Send
        // -------------------------------------------------------------------

        [Fact]
        public async Task Send_ReturnsBadRequest_WhenBodyIsNull()
        {
            IActionResult response = await CreateSut()
                .Send(TransferTestData.WalletId, null!, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(response);

            _orchestrator.Verify(
                o => o.SendAsync(It.IsAny<SendModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Send_ReturnsOk_OnDryRun()
        {
            _orchestrator
                .Setup(o => o.SendAsync(It.IsAny<SendModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.SendResult());

            IActionResult response = await CreateSut()
                .Send(TransferTestData.WalletId, ValidSendDto(), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var dto = Assert.IsType<SendDtoResponse>(ok.Value);

            // A dry run must not report a txid - nothing was broadcast.
            Assert.Null(dto.TxId);
            Assert.False(dto.Broadcast);
        }

        [Fact]
        public async Task Send_MapsRouteWalletIdIntoTheModel()
        {
            SendModel? captured = null;

            _orchestrator
                .Setup(o => o.SendAsync(It.IsAny<SendModel>(), It.IsAny<CancellationToken>()))
                .Callback<SendModel, CancellationToken>((m, _) => captured = m)
                .ReturnsAsync(TransferTestData.SendResult());

            await CreateSut().Send(TransferTestData.WalletId, ValidSendDto(), CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(TransferTestData.WalletId, captured.WalletId);
            Assert.Equal(TransferTestData.ToAddress, captured.ToAddress);
        }

        [Fact]
        public async Task Send_ReturnsBadRequest_OnArgumentException()
        {
            _orchestrator
                .Setup(o => o.SendAsync(It.IsAny<SendModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Amount must be greater than zero."));

            Assert.IsType<BadRequestObjectResult>(
                await CreateSut().Send(TransferTestData.WalletId, ValidSendDto(), CancellationToken.None));
        }

        [Fact]
        public async Task Send_ReturnsConflict_OnInsufficientFunds()
        {
            _orchestrator
                .Setup(o => o.SendAsync(It.IsAny<SendModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Insufficient funds."));

            var conflict = Assert.IsType<ConflictObjectResult>(
                await CreateSut().Send(TransferTestData.WalletId, ValidSendDto(), CancellationToken.None));

            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        }

        [Fact]
        public async Task Send_Returns502_WhenBroadcastEndpointUnreachable()
        {
            _orchestrator
                .Setup(o => o.SendAsync(It.IsAny<SendModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("timeout"));

            var status = Assert.IsType<ObjectResult>(
                await CreateSut().Send(TransferTestData.WalletId, ValidSendDto(), CancellationToken.None));

            Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        }

        // -------------------------------------------------------------------
        // CreateDeposit
        // -------------------------------------------------------------------

        [Fact]
        public async Task CreateDeposit_ReturnsCreated_WithLocationHeader()
        {
            _orchestrator
                .Setup(o => o.CreateDepositeAsync(It.IsAny<CreateDepositModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.DepositResult());

            IActionResult response = await CreateSut()
                .CreateDeposit(TransferTestData.WalletId, ValidDepositDto(), CancellationToken.None);

            var created = Assert.IsType<CreatedResult>(response);
            Assert.Contains(TransferTestData.DepositId, created.Location);

            var dto = Assert.IsType<DepositDtoResponse>(created.Value);
            Assert.Equal(DepositStatus.Pending.ToString(), dto.Status);
            Assert.NotEmpty(dto.PaymentUri);
        }

        [Fact]
        public async Task CreateDeposit_ReturnsBadRequest_WhenBodyIsNull()
        {
            Assert.IsType<BadRequestObjectResult>(
                await CreateSut().CreateDeposit(TransferTestData.WalletId, null!, CancellationToken.None));
        }

        [Fact]
        public async Task CreateDeposit_ReturnsBadRequest_WhenAmountBelowDust()
        {
            _orchestrator
                .Setup(o => o.CreateDepositeAsync(It.IsAny<CreateDepositModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Expected amount is below the dust threshold."));

            Assert.IsType<BadRequestObjectResult>(
                await CreateSut().CreateDeposit(TransferTestData.WalletId, ValidDepositDto(), CancellationToken.None));
        }

        [Fact]
        public async Task CreateDeposit_ReturnsNotFound_WhenWalletMissing()
        {
            _orchestrator
                .Setup(o => o.CreateDepositeAsync(It.IsAny<CreateDepositModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            Assert.IsType<NotFoundResult>(
                await CreateSut().CreateDeposit("missing", ValidDepositDto(), CancellationToken.None));
        }

        // -------------------------------------------------------------------
        // GetDeposit / GetDeposits
        // -------------------------------------------------------------------

        [Fact]
        public async Task GetDeposit_ReturnsOk_WithStatus()
        {
            _orchestrator
                .Setup(o => o.GetDepositAsync(TransferTestData.WalletId, TransferTestData.DepositId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TransferTestData.DepositResult(DepositStatus.Confirmed));

            IActionResult response = await CreateSut()
                .GetDeposit(TransferTestData.WalletId, TransferTestData.DepositId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var dto = Assert.IsType<DepositDtoResponse>(ok.Value);

            Assert.Equal("Confirmed", dto.Status);
        }

        [Fact]
        public async Task GetDeposit_ReturnsNotFound_WhenUnknown()
        {
            _orchestrator
                .Setup(o => o.GetDepositAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            Assert.IsType<NotFoundResult>(
                await CreateSut().GetDeposit(TransferTestData.WalletId, "missing", CancellationToken.None));
        }

        [Fact]
        public async Task GetDeposits_ReturnsEveryDeposit_NotAnEmptyList()
        {
            _orchestrator
                .Setup(o => o.GetDepositsAsync(TransferTestData.WalletId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    TransferTestData.DepositResult(),
                    TransferTestData.DepositResult(DepositStatus.Confirmed),
                    TransferTestData.DepositResult(DepositStatus.Expired)
                ]);

            IActionResult response = await CreateSut()
                .GetDeposits(TransferTestData.WalletId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var payload = Assert.IsAssignableFrom<IEnumerable<DepositDtoResponse>>(ok.Value);

            Assert.Equal(3, payload.Count());
        }

        [Fact]
        public async Task GetDeposits_ReturnsEmptyList_WhenNone()
        {
            _orchestrator
                .Setup(o => o.GetDepositsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var ok = Assert.IsType<OkObjectResult>(
                await CreateSut().GetDeposits(TransferTestData.WalletId, CancellationToken.None));

            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<DepositDtoResponse>>(ok.Value));
        }

        // -------------------------------------------------------------------

        [Fact]
        public void Constructor_ThrowsWhenOrchestratorIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new WalletTransferController(null!));
        }

        private static SendDto ValidSendDto() => new()
        {
            ToAddress = TransferTestData.ToAddress,
            AmountSats = 50_000,
            Passphrase = string.Empty,
            SweepAll = false,
            DryRun = true,
            IdempotencyKey = TransferTestData.IdempotencyKey
        };

        private static CreateDepositDto ValidDepositDto() => new()
        {
            ExpectedSats = 50_000,
            Label = "Invoice 1234",
            ExpiryMinutes = 1440
        };
    }
}