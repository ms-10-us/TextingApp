using BitcoinWalletMicroService.Entities;
using System.Data;

namespace BitcoinWalletMicroService.Repository
{
    public interface IWalletRepository
    {
        Task<WalletEntity?> GetWalletByFingerprintAsync(string fingerprint, CancellationToken ct = default(CancellationToken));

        Task<string> InsertWalletAsync(
            WalletEntity wallet, 
            IDbTransaction transaction = null, 
            CancellationToken ct = default(CancellationToken));

        Task InsertDerivedKeyAsync(
            IEnumerable<DerivedKeyEntity> keys,
            IDbTransaction transaction = null,
            CancellationToken ct = default(CancellationToken));

        Task<IEnumerable<WalletSummaryEntity>> GetWalletSummariesAsync(CancellationToken ct = default(CancellationToken));

        Task<WalletEntity> GetWalletByIdAsync(string walletId,  CancellationToken ct = default(CancellationToken));

        Task<IEnumerable<DerivedKeyEntity>> GetDerivedKeysAsync(
            string walletId, 
            bool? isChange, 
            CancellationToken ct = default(CancellationToken));

        Task<bool> DeleteWalletAsync(string walletId, CancellationToken ct = default(CancellationToken));

        Task<int> GetNextAddressIndexAsync(
            string walletId,
            bool isChange,
            CancellationToken ct = default(CancellationToken));
    }
}
