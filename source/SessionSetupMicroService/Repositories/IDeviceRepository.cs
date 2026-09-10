using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Repositories
{
    public interface IDeviceRepository
    {
        Task EnsureAccountAsync(AccountId account, CancellationToken ct = default);

        Task InsertAsync(ProtocolAddress address, RegisterDeviceModel registration, byte[] credentialHash, CancellationToken ct = default);

        Task<Device?> FindAsync(ProtocolAddress address, CancellationToken ct = default);
    }
}
