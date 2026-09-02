using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Mappings;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using Microsoft.AspNetCore.Mvc;

namespace BitcoinWalletMicroService.Controllers
{
    [ApiController]
    [Route("api/wallets")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletOrchestrator _orchestrator;

        public WalletController(IWalletOrchestrator orchestrator)
        {
            if (orchestrator == null)
            {
                throw new ArgumentNullException("Orchestrator can't be null");
            }
            _orchestrator = orchestrator;
        }

        [HttpPost("CreateWallet")]
        [Route("")]
        public async Task<IActionResult> Create(
            [FromBody] CreateWalletDto request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                CreateWalletModel model = request.ToModel();
                CreateWalletResult result = await _orchestrator.CreateWalletAsync(model, cancellationToken);
                return Created($"/api/wallets/{result.WalletId}", result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }
    }
}
