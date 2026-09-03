using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Mappings;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using Microsoft.AspNetCore.Mvc;
using System.Collections;

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
                CreateWalletDtoResponse response = result.ToDto();
                return Created($"/api/wallets/{response.WalletId}", response);
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

        [HttpPost("ImportWallet")]
        public async Task<IActionResult> Import(
            [FromBody] ImportWalletDto request,
            CancellationToken ct)
        {

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                ImportWalletModel model = request.ToModel();
                CreateWalletResult result = await _orchestrator.ImportWalletAsync(model, ct);
                CreateWalletDtoResponse response = result.ToDto();
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }

        }

        [HttpGet("GetAllWallets")]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            IEnumerable<WalletSummary> wallets = await _orchestrator.GetWalletsAsync(ct);
            IEnumerable<WalletSummaryDtoResponse> response = new List<WalletSummaryDtoResponse>();
            foreach(WalletSummary wallet in wallets)
            {
                response.Append(wallet.ToDto());
            }

            return Ok(wallets);
        }

        [HttpGet("GetWalletById/{walletId}")]
        public async Task<IActionResult> GetById(string walletId, CancellationToken ct)
        {
            WalletSummary wallet = await _orchestrator.GetWalletAsync(walletId, ct);
            if (wallet == null)
            {
                return NotFound();
            }

            return Ok(wallet.ToDto());
        }

        [HttpGet("GetAddresses/{walletId}")]
        public async Task<IActionResult> GetAddresses(
            string walletId,
            bool? isChange,
            CancellationToken ct)
        {
            WalletSummary wallet = await _orchestrator.GetWalletAsync(walletId, ct);
            if (wallet == null)
            {
                return NotFound();
            }

            IEnumerable<DerivedKeyResult> addresses = await _orchestrator.GetAddressesAsync(walletId, isChange, ct).ConfigureAwait(false);
            IEnumerable<DerivedKeyDtoResponse> response = new List<DerivedKeyDtoResponse>();
            foreach (DerivedKeyResult address in addresses)
            {
                response.Append(address.ToDto());
            }

            return Ok(response);
        }

        [HttpDelete("DeleteWallet/{walletId}")]
        public async Task<IActionResult> Delete(string walletId, CancellationToken ct)
        {
            bool deleted = await _orchestrator.DeleteWalletAsync(walletId, ct);
            if (!deleted)
            {
                return NotFound();
            }

            return StatusCode(StatusCodes.Status204NoContent);
        }
    }
}
