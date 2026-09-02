using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Orchestrator
{
    public interface IWalletOrchestrator
    {
        Task<CreateWalletResult> CreateWalletAsync(CreateWalletModel model, CancellationToken ct = default(CancellationToken));
    }
}
