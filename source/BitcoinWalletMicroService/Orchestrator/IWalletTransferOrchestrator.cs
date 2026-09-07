using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Orchestrator
{
    public interface IWalletTransferOrchestrator
    {
        Task<ReceiveAddressResult> GetNextReceiveAddressAsync(string walletId, bool isChange = false, CancellationToken ct = default);
        
        Task<WalletBalance> GetBalanceAsync(string walletId, CancellationToken ct = default);

        Task<SendResult> SendAsync(SendModel model, CancellationToken ct = default(CancellationToken));

        Task<IEnumerable<SendTransaction>> GetSendTransactionAsync(string walletId, CancellationToken ct = default (CancellationToken));
    }
}
