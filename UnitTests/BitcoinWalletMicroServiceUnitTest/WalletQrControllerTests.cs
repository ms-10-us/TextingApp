using BitcoinWalletMicroService.Controllers;
using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using BitcoinWalletMicroService.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinWalletMicroServiceUnitTest
{
    public class WalletQrControllerTests
    {
        private readonly Mock<IWalletOrchestrator> _orchestrator = new Mock<IWalletOrchestrator>();
        private readonly Mock<IQrCodeService> _qrCodeService = new Mock<IQrCodeService>();

        private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47];

        private WalletQrController CreateSut()
        {
            var controller = new WalletQrController(_orchestrator.Object, _qrCodeService.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            return controller;
        }

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressQr_ThenReturnsSvgByDefault()
        {
            SetupAddress();
            _qrCodeService.Setup(q => q.GenerateSvg(It.IsAny<string>(), It.IsAny<int>())).Returns("<svg/>");

            IActionResult response = await CreateSut()
                .GetAddressQr(TestData.WalletId, 0, CancellationToken.None);

            var content = Assert.IsType<ContentResult>(response);
            Assert.Equal("image/svg+xml", content.ContentType);
        }

        [Fact]
        public async Task GivenFormatRequest_WhenCallingGetAddressQr_ThenReturnsFormatRequested()
        {
            SetupAddress();
            _qrCodeService.Setup(q => q.GeneratePng(It.IsAny<string>(), It.IsAny<int>())).Returns(FakePng);

            IActionResult response = await CreateSut()
                .GetAddressQr(TestData.WalletId, 0, CancellationToken.None, format: "png");

            var file = Assert.IsType<FileContentResult>(response);
            Assert.Equal("image/png", file.ContentType);
        }

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressQr_ThenFormatIsCaseInsensitive()
        {
            SetupAddress();
            _qrCodeService.Setup(q => q.GeneratePng(It.IsAny<string>(), It.IsAny<int>())).Returns(FakePng);

            IActionResult response = await CreateSut()
                .GetAddressQr(TestData.WalletId, 0, CancellationToken.None, format: "PNG");

            Assert.IsType<FileContentResult>(response);
        }

        [Fact]
        public async Task GivenNegativeIndex_WhenCallingGetAddressQr_ReturnsBadRequest()
        {
            IActionResult response = await CreateSut()
                .GetAddressQr(TestData.WalletId, -1, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(response);

            _orchestrator.Verify(
                o => o.GetAddressesAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GivenIndexDoesNotExist_WhenCallingGetAddressQr_ReturnsNotFound()
        {
            _orchestrator
                .Setup(o => o.GetAddressesAsync(TestData.WalletId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync([TestData.KeyResult(0)]);

            IActionResult response = await CreateSut()
                .GetAddressQr(TestData.WalletId, 99, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(response);
        }

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressQr_ThenEncodesStoredAddress()
        {
            DerivedKeyResult stored = TestData.KeyResult(0);
            SetupAddress(stored);

            string? captured = null;
            _qrCodeService
                .Setup(q => q.GenerateSvg(It.IsAny<string>(), It.IsAny<int>()))
                .Callback<string, int>((payload, _) => captured = payload)
                .Returns("<svg/>");

            await CreateSut().GetAddressQr(TestData.WalletId, 0, CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Contains(stored.Address, captured, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GivenAmount_WhenCallingGetAddressQr_ThenIncludesAmountInPaymnetUri()
        {
            SetupAddress();

            string? captured = null;
            _qrCodeService
                .Setup(q => q.GenerateSvg(It.IsAny<string>(), It.IsAny<int>()))
                .Callback<string, int>((payload, _) => captured = payload)
                .Returns("<svg/>");

            await CreateSut().GetAddressQr(TestData.WalletId, 0, CancellationToken.None, amount: 0.001m);

            Assert.NotNull(captured);
            Assert.Contains("amount=0.001", captured);
        }

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressQr_ThenSetsImmutableCacheHeader()
        {
            SetupAddress();
            _qrCodeService.Setup(q => q.GenerateSvg(It.IsAny<string>(), It.IsAny<int>())).Returns("<svg/>");

            WalletQrController sut = CreateSut();
            await sut.GetAddressQr(TestData.WalletId, 0, CancellationToken.None);

            Assert.Contains("immutable", sut.Response.Headers.CacheControl.ToString());
        }

        [Fact]
        public async Task GivenAddress_WhenCallingGetAddressQrData_ThenReturnsBase64FataUri()
        {
            SetupAddress();
            _qrCodeService.Setup(q => q.GeneratePng(It.IsAny<string>(), It.IsAny<int>())).Returns(FakePng);

            IActionResult response = await CreateSut()
                .GetAddressQrData(TestData.WalletId, 0, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response);
            var dto = Assert.IsType<AddressQrDtoResponse>(ok.Value);

            Assert.StartsWith("data:image/png;base64,", dto.QrPngDataUri);
            Assert.Equal(Convert.ToBase64String(FakePng), dto.QrPngDataUri.Split(',')[1]);
            Assert.Equal(TestData.KeyResult(0).Address, dto.Address);
        }


        private void SetupAddress(DerivedKeyResult? address = null)
        {
            _orchestrator
                .Setup(o => o.GetAddressesAsync(TestData.WalletId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync([address ?? TestData.KeyResult(0)]);
        }
    }
}
