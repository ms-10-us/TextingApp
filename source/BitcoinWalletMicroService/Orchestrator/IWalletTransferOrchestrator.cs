using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Orchestrator
{
    public interface IWalletTransferOrchestrator
    {
        Task<ReceiveAddressResult> GetNextReceiveAddressAsync(string walletId, bool isChange = false, CancellationToken ct = default); 
    }
}
