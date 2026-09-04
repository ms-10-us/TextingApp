using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using BitcoinWalletMicroService.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace BitcoinWalletMicroService.Controllers
{
    [ApiController]
    [Route("api/wallets")]
    public class WalletQrController : ControllerBase
    {
        private readonly IWalletOrchestrator _orchestrator;
        private readonly IQrCodeService _qrCodeService;

        public WalletQrController(IWalletOrchestrator orchestrator, IQrCodeService qrCodeService)
        {
            ArgumentNullException.ThrowIfNull(orchestrator);
            ArgumentNullException.ThrowIfNull(qrCodeService);

            _orchestrator = orchestrator;
            _qrCodeService = qrCodeService;
        }

        [HttpGet("{walletId}/addresses/{index:int}/qr")]
        [Produces("image/svg+xml", "image/png")]
        public async Task<IActionResult> GetAddressQr(
            string walletId,
            int index,
            CancellationToken ct,
            [FromQuery] bool isChange = false,
            [FromQuery] string format = "svg",
            [FromQuery] int size = 10,
            [FromQuery] decimal? amount = null,
            [FromQuery] string? label = null)
        {
            if (index < 0)
            {
                return BadRequest("Index cannot be negative.");
            }

            IEnumerable<DerivedKeyResult> addresses =
                await _orchestrator.GetAddressesAsync(walletId, isChange, ct);

            DerivedKeyResult? target = addresses.FirstOrDefault(a => a.AddressIndex == index);

            if (target == null)
            {
                return NotFound($"No address at index {index} for this wallet.");
            }

            string payload = BipUriBuilder.Build(target.Address, amount, label);

            Response.Headers.CacheControl = "private, max-age=31536000, immutable";

            if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
            {
                byte[] png = _qrCodeService.GeneratePng(payload, size);
                return File(png, "image/png");
            }

            string svg = _qrCodeService.GenerateSvg(payload, size);
            return Content(svg, "image/svg+xml");
        }

        [HttpGet("{walletId}/addresses/{index:int}/qr-data")]
        public async Task<IActionResult> GetAddressQrData(string walletId,
            int index,
            CancellationToken ct,
            [FromQuery] bool isChange = false,
            [FromQuery] string format = "svg",
            [FromQuery] int size = 10,
            [FromQuery] decimal? amount = null,
            [FromQuery] string? label = null)
        {
            if (index < 0)
            {
                return BadRequest("Index cannot be negative.");
            }

            IEnumerable<DerivedKeyResult> addresses = await _orchestrator.GetAddressesAsync(walletId, isChange, ct);

            DerivedKeyResult? target = addresses.FirstOrDefault(a => a.AddressIndex == index);

            if (target == null)
            {
                return NotFound($"No address at index {index} for this wallet");
            }

            string payload = BipUriBuilder.Build(target.Address, amount, label);
            byte[] png = _qrCodeService.GeneratePng(payload, size);

            return Ok(new AddressQrDtoResponse
            {
                Address = target.Address,
                DerivationPath = target.DerivationPath,
                AddressIndex = target.AddressIndex,
                IsChange = target.IsChange,
                PaymentUri = payload,
                QrPngDataUri = $"data:image/png;base64,{Convert.ToBase64String(png)}"
            });
        }
    }
}
