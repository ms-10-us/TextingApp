using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;

namespace SessionSetupMicroService.Orchestrators
{
    public interface IDeviceRegistrationOrchestrator
    {
        Task<Result<DeviceRegistrationResult>> RegisterAsync(RegisterDeviceModel registration, CancellationToken ct = default);

        Task<Result<Device>> GetAsync(ProtocolAddress address, CancellationToken ct = default);

        Task<Result<IEnumerable<Device>>> ListAsync(AccountId account, CancellationToken ct = default);

        Task<Result<DeviceRegistrationResult>> LinkDeviceAsync(
            AccountId account,
            DeviceId vouchingDevice,
            string? vouchingCredential,
            RegisterDeviceModel registration,
            CancellationToken ct = default);
    }
}
