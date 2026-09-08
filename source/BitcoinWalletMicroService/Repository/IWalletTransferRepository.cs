using BitcoinWalletMicroService.Entities;

namespace BitcoinWalletMicroService.Repository
{
    public interface IWalletTransferRepository
    {
        Task<SendTransactionEntity?> GetSendTransactionByIdempotencyKeyAsync(string walletId, string idempotencyKey, CancellationToken ct);

        Task InsertSentTransactionAsync(SendTransactionEntity entity, CancellationToken ct);

        Task<IEnumerable<SendTransactionEntity>> GetSendTransactionAsync(string walletId, CancellationToken ct);

        Task InsertDepositAsync(DepositEntity deposit, CancellationToken ct = default(CancellationToken));

        Task<DepositEntity?> GetDepositByIdAsync(
            string walletId,
            string depositId,
            CancellationToken ct = default(CancellationToken));

        Task UpdateDepositAsync(DepositEntity deposit, CancellationToken ct = default(CancellationToken));

        Task<IEnumerable<DepositEntity>> GetDepositsAsync(
            string walletId, CancellationToken ct = default(CancellationToken));
    }
}
