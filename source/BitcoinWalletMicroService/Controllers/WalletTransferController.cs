using BitcoinWalletMicroService.Dtos;
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

        [HttpGet("GetBalance/{walletId}")]
        public async Task<IActionResult> GetBalance(string walletId, CancellationToken ct)
        {
            try
            {
                WalletBalance balance = await _orchestrator.GetBalanceAsync(walletId, ct);
                return Ok(balance.ToDto());
            }
            catch(KeyNotFoundException)
            {
                return NotFound();
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    $"Blockchain data source unavailable: {ex.Message}");
            }
        }

        [HttpPost("Send/{walletId}")]
        public async Task<IActionResult> Send(
            string walletId,
            [FromBody] SendDto request, 
            CancellationToken ct)
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
                SendModel model = request.ToModel(walletId);
                SendResult result = await _orchestrator.SendAsync(model, ct);

                return Ok(result.ToDto());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Insufficient funds, no confirmed outputs, or a rejected broadcast.
                return Conflict(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    $"Blockchain data source unavailable: {ex.Message}");
            }
        }
    }
}
