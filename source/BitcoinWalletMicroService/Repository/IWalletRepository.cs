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

    }
}
