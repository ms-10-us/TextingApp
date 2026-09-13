using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Security;

namespace SessionSetupMicroService.Orchestrators
{
    public class DeviceAuthenticator : IDeviceAuthenticator
    {
        private readonly IDeviceRepository _devices;
        private readonly IDeviceCredentialHasher _credentialHasher;

        public DeviceAuthenticator(IDeviceRepository devices,  IDeviceCredentialHasher credentialHasher)
        {
            _devices = devices;
            _credentialHasher = credentialHasher;
        }

        public async Task<Result<ProtocolAddress>> AuthenticateAsync(
            ProtocolAddress address, 
            string? credential, 
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(credential))
            {
                return Result<ProtocolAddress>.Failure(SessionSetupError.Unauthorized());
            }

            var storedHash = await _devices.GetCredentialHashAsync(address, ct);

            if (storedHash == null || !_credentialHasher.Verify(credential, storedHash))
            {
                return Result<ProtocolAddress>.Failure(SessionSetupError.Unauthorized());
            }

            return Result<ProtocolAddress>.Success(address);
        }
    }
}
