using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;

namespace SessionSetupMicroService.Orchestrators
{
    public interface IDeviceAuthenticator
    {
        Task<Result<ProtocolAddress>> AuthenticateAsync(ProtocolAddress address, string? credential, CancellationToken ct = default);
    }
}
