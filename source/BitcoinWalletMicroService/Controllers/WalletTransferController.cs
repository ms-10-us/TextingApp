using BitcoinWalletMicroService.Mappings;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using Microsoft.AspNetCore.Mvc;

namespace BitcoinWalletMicroService.Controllers
{
    [ApiController]
    [Route("api/wallets")]
    public class WalletTransferController : ControllerBase
    {
        private readonly IWalletTransferOrchestrator _orchestrator;

        public WalletTransferController(IWalletTransferOrchestrator orchestrator)
        {
            if (orchestrator == null)
            {
                throw new ArgumentNullException("Orchestrator can't be null");
            }

            _orchestrator = orchestrator;
        }

        [HttpPost("GetReceiveAddress/{walletId}")]
        public async Task<IActionResult> GetReceiveAddress(string walletId, CancellationToken ct)
        {
            try
            {
                ReceiveAddressResult result = await _orchestrator
                    .GetNextReceiveAddressAsync(walletId, false, ct);

                return Ok(result.ToDto());
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }






















    }
}
