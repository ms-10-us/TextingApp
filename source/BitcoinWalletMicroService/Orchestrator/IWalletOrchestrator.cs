using BitcoinWalletMicroService.Dtos;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Orchestrator
{
    public interface IWalletOrchestrator
    {
        Task<CreateWalletResult> CreateWalletAsync(CreateWalletModel model, CancellationToken ct = default(CancellationToken));

        Task<CreateWalletResult> ImportWalletAsync(ImportWalletModel model, CancellationToken ct = default(CancellationToken));

        Task<IEnumerable<WalletSummary>> GetWalletsAsync(CancellationToken ct = default(CancellationToken));

        Task<WalletSummary> GetWalletAsync(string walletId, CancellationToken ct = default(CancellationToken));

        Task<IEnumerable<DerivedKeyResult>> GetAddressesAsync(
            string walletId,
            bool? isChange = null,
            CancellationToken ct = default(CancellationToken));

        Task<bool> DeleteWalletAsync(string walletId, CancellationToken ct);
    }
}
