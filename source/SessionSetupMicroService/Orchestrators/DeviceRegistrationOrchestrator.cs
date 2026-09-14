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
        private readonly IDeviceAuthenticator _authenticator;
        private readonly IOptions<PreKeyPolicyOptions> _policy;
        private readonly TimeProvider _clock;
        private readonly ILogger<DeviceRegistrationOrchestrator> _logger;

        public DeviceRegistrationOrchestrator(
            IUnitOfWork unitOfWork,
            IDeviceRepository devices,
            IPreKeyRepository preKeys,
            IDeviceCredentialHasher credentials,
            IDeviceAuthenticator authenticator,
            IOptions<PreKeyPolicyOptions> policy,
            TimeProvider clock,
            ILogger<DeviceRegistrationOrchestrator> logger)
        {
            _unitOfWork = unitOfWork;
            _devices = devices;
            _preKeys = preKeys;
            _credentials = credentials;
            _authenticator = authenticator;
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

        public async Task<Result<IEnumerable<Device>>> ListAsync(AccountId account, CancellationToken ct = default)
        {
            var found = await _devices.ListByAccountAsync(account, ct);
            if (found.Count() == 0)
            {
                return Result<IEnumerable<Device>>.Failure(SessionSetupError.DeviceNotFound(account));
            }

            return Result<IEnumerable<Device>>.Success(found);
        }

        public async Task<Result<DeviceRegistrationResult>> LinkDeviceAsync(AccountId account, DeviceId vouchingDevice, string? vouchingCredential, RegisterDeviceModel registration, CancellationToken ct = default)
        {
            if (KeyMaterialValidator.ValidateRegistration(registration, _policyValue.MaxKeysPerUpload) is { } problem)
            {
                return Result<DeviceRegistrationResult>.Failure(problem);
            }

            var vouchingAddress = new ProtocolAddress(account, vouchingDevice);

            Result<ProtocolAddress> authentication = await _authenticator.AuthenticateAsync(vouchingAddress, vouchingCredential, ct);

            if (!authentication.IsSuccess)
            {
                _logger.LogWarning("Rejected a device link for account {Account} vouched by {Address}", account, vouchingAddress);
                return Result<DeviceRegistrationResult>.Failure(authentication.Error!);
            }

            var credential = _credentials.Generate();
            var credentialHash = _credentials.Hash(credential);

            Result<DeviceRegistrationResult> outcome = await _unitOfWork.ExecuteAsync(async token =>
            {
                if (await _devices.LockAccountAsync(account, token))
                {
                    return Result<DeviceRegistrationResult>.Failure(SessionSetupError.DeviceNotFound(account));
                }

                Device? vouching = await _devices.FindAsync(vouchingAddress, token);
                if (vouching == null)
                {
                    return Result<DeviceRegistrationResult>.Failure(SessionSetupError.InvalidKeyMaterial(
                        "A linked device must present the same identity key as the account's existing devices."));
                }

                DeviceId deviceId = await _devices.NextDeviceIdAsync(account, token);
                var address = new ProtocolAddress(account, deviceId);

                if (!await _devices.InsertAsync(address, registration, credentialHash, token))
                {
                    return Result<DeviceRegistrationResult>.Failure(SessionSetupError.DeviceAlreadyRegistered(address));
                }

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

                return Result<DeviceRegistrationResult>.Success(new DeviceRegistrationResult(device, credential));

            }, ct);

            if (!outcome.IsSuccess)
            {
                return outcome;
            }

            ProtocolAddress linked = outcome.Value!.Device.Address;

            _logger.LogInformation(
            "Linked {Address} to account {Account}, vouched by {Vouching}, with {CurveCount} curve and {KyberCount} Kyber one-time prekeys",
            linked, account, vouchingAddress, registration.OneTimePreKeys.Count(), registration.OneTimeKyberPreKeys.Count());

            WarnAboutLowPool(linked, registration);

            return outcome;
        }

        private void WarnAboutLowPool(ProtocolAddress address, RegisterDeviceModel registration)
        {
            if (registration.OneTimePreKeys.Count() >= _policyValue.LowWaterMark)
            {
                return;
            }

            _logger.LogWarning("{Address} registered with {Count} one-time prekeys, below the low-water mark of {LowWaterMark}; " +
            "senders will start losing DH4 quickly",
            address, registration.OneTimePreKeys.Count(), _policyValue.LowWaterMark);
        }
    }
}
