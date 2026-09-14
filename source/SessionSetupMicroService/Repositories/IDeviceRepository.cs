using SessionSetupMicroService.Models;
using SessionSetupMicroService.PostgresDB;

namespace SessionSetupMicroService.Repositories
{
    public interface IDeviceRepository
    {
        Task EnsureAccountAsync(AccountId account, CancellationToken ct = default);

        Task<bool> InsertAsync(ProtocolAddress address, RegisterDeviceModel registration, byte[] credentialHash, CancellationToken ct = default);

        Task<Device?> FindAsync(ProtocolAddress address, CancellationToken ct = default);

        Task<IEnumerable<Device>> ListByAccountAsync(AccountId account, CancellationToken ct = default);

        Task<byte[]?> GetCredentialHashAsync(ProtocolAddress address, CancellationToken ct = default);

        Task<bool> ExistsAsync(ProtocolAddress address, CancellationToken ct = default); 
        
        Task TouchAsync(ProtocolAddress address, CancellationToken ct = default);

        Task<bool> LockAccountAsync(AccountId account, CancellationToken ct = default);

        Task<DeviceId> NextDeviceIdAsync(AccountId account, CancellationToken ct = default);
      
    }
}
