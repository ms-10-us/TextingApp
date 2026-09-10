using Microsoft.Extensions.Options;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Options;
using SessionSetupMicroService.PostgresDB;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Security;
using SessionSetupMicroService.Validators;

namespace SessionSetupMicroService.Orchestrators
{
    public class DeviceRegistrationOrchestrator : IDeviceRegistrationOrchestrator
    {
        private readonly PreKeyPolicyOptions _policyValue;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDeviceRepository _devices;
        private readonly IPreKeyRepository _preKeys;
        private readonly IDeviceCredentialHasher _credentials;
        private readonly IOptions<PreKeyPolicyOptions> _policy;
        private readonly TimeProvider _clock;
        private readonly ILogger<DeviceRegistrationOrchestrator> _logger;

        public DeviceRegistrationOrchestrator(
            IUnitOfWork unitOfWork,
            IDeviceRepository devices,
            IPreKeyRepository preKeys,
            IDeviceCredentialHasher credentials,
            IOptions<PreKeyPolicyOptions> policy,
            TimeProvider clock,
            ILogger<DeviceRegistrationOrchestrator> logger)
        {
            _unitOfWork = unitOfWork;
            _devices = devices;
            _preKeys = preKeys;
            _credentials = credentials;
            _policy = policy;
            _clock = clock;
            _logger = logger;

            _policyValue = _policy.Value;
        }

        public async Task<Result<DeviceRegistrationResult>> RegisterAsync(RegisterDeviceModel registration, CancellationToken ct = default)
        {
            if (KeyMaterialValidator.ValidateRegistration(registration, _policyValue.MaxKeysPerUpload) is { } problem)
            {
                return Result<DeviceRegistrationResult>.Failure(problem);
            }

            var address = new ProtocolAddress(AccountId.New(), new DeviceId(DeviceId.Primary));
            var credential = _credentials.Generate();
            var creadentialHash = _credentials.Hash(credential);

            var device = await _unitOfWork.ExecuteAsync(async token =>
            {
                await _devices.EnsureAccountAsync(address.Account, token);
                await _devices.InsertAsync(address, registration, creadentialHash, token);

                await _preKeys.UpsertSignedPreKeyAsync(address, registration.SignedPreKey, token);
                await _preKeys.UpsertSignedPreKeyAsync(address, registration.LastResortKyberPreKey, token);
                await _preKeys.AddOneTimePreKeysAsync(address, registration.OneTimePreKeys, token);
                await _preKeys.AddOneTimePreKeysAsync(address, registration.OneTimeKyberPreKeys, token);

                var now = _clock.GetUtcNow();
                Device device = new Device
                {
                    Address = address,
                    DisplayName = registration.DisplayName,
                    RegistrationId = registration.RegistrationId,
                    IdentityKey = registration.IdentityKey,
                    RegisteredAt = now,
                    LastSeen = now
                };                   

                return device;

            }, ct);

            _logger.LogInformation("Registered {Address} with {CurveCount} curve and {KyberCount} Kyber one-time prekeys",
            address, registration.OneTimePreKeys.Count(), registration.OneTimeKyberPreKeys.Count());

            if (registration.OneTimePreKeys.Count() < _policyValue.LowWaterMark)
            {
                _logger.LogWarning("{Address} registered with {Count} one-time prekeys, below the low-water mark of {LowWaterMark}; " +
                "senders will start losing DH4 quickly",
                address, registration.OneTimePreKeys.Count(), _policyValue.LowWaterMark);
            }

            Result<DeviceRegistrationResult> result = Result<DeviceRegistrationResult>
                .Success(new DeviceRegistrationResult(device, credential));
            return result;
        }

        public async Task<Result<Device>> GetAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            Device? device = await _devices.FindAsync(address, ct);
            if (device == null)
            {
                return Result<Device>.Failure(SessionSetupError.DeviceNotFound(address));
            }
            else
            {
                return Result<Device>.Success(device);
            }
        }
    }
}
