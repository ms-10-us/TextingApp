using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Orchestrator
{
    public interface IWalletOrchestrator
    {
        Task<CreateWalletResult> CreateWalletAsync(CreateWalletDto request, CancellationToken ct = default(CancellationToken));
    }
}
