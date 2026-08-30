using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;
using BitcoinWalletMicroService.Orchestrator;
using System.Web.Http;

namespace BitcoinWalletMicroService.Controllers
{
    [RoutePrefix("api/wallets")]
    public class WalletController : ApiController
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

        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Create(
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
                CreateWalletResult result = await _orchestrator.CreateWalletAsync(request, cancellationToken);
                return Created(new Uri(Request.RequestUri!, "/api/wallets/" + result.WalletId), result);
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

        private IHttpActionResult Conflict(string message)
        {
            return Content(System.Net.HttpStatusCode.Conflict, message);
        }

    }
}
