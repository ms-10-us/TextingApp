using Microsoft.Extensions.Options;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;
using SessionSetupMicroService.Options;
using SessionSetupMicroService.PostgresDB;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Validators;
using System.Net;

namespace SessionSetupMicroService.Orchestrators
{
    public class PreKeyOrchestrator : IPreKeyOrchestrator
    {
        private readonly IUnitOfWork _unitOfWork;

        private readonly IDeviceRepository _devices;

        private readonly IPreKeyRepository _preKeys;

        private readonly IOptions<PreKeyPolicyOptions> _policy;

        private readonly TimeProvider _clock;

        private readonly ILogger<PreKeyOrchestrator> _logger;

        private readonly PreKeyPolicyOptions _policyValue;

        public PreKeyOrchestrator(
            IUnitOfWork unitOfWork,
            IDeviceRepository devices,
            IPreKeyRepository preKeys,
            IOptions<PreKeyPolicyOptions> policy,
            TimeProvider clock,
            ILogger<PreKeyOrchestrator> logger)
        {
            _unitOfWork = unitOfWork;
            _devices = devices;
            _preKeys = preKeys;
            _policy = policy;
            _clock = clock;
            _logger = logger;
            _policyValue = _policy.Value;
        }

        public async Task<Result<PreKeyBundle>> TakeBundleAsync(ProtocolAddress protocolAddress, CancellationToken ct = default)
        {
            Device? device = await _devices.FindAsync(protocolAddress, ct);
            if (device == null)
            {
                return Result<PreKeyBundle>.Failure(SessionSetupError.DeviceNotFound(protocolAddress));
            }

            PreKeyBundle? assembled = await _unitOfWork.ExecuteAsync(async token =>
            {
                SignedPreKey? signedPreKey = await _preKeys.GetSignedPreKeyAsync(protocolAddress, PreKeyKind.Curve, token);
                SignedPreKey? lastResortKyber = await _preKeys.GetSignedPreKeyAsync(protocolAddress, PreKeyKind.Kyber, token);
                if (signedPreKey == null || lastResortKyber == null)
                {
                    return null;
                }

                OneTimePreKey? oneTimeCurve = await _preKeys.TakeOneTimePreKeyAsync(protocolAddress, PreKeyKind.Curve, token);
                OneTimePreKey? oneTimeKyber = await _preKeys.TakeOneTimePreKeyAsync(protocolAddress, PreKeyKind.Kyber, token);

                var servedLastResort = oneTimeKyber is null;
                var kyberPreKey = oneTimeKyber is null
                    ? lastResortKyber
                    : new SignedPreKey
                    {
                        Kind = PreKeyKind.Kyber,
                        Id = oneTimeKyber.Id,
                        PublicKey = oneTimeKyber.PublicKey,
                        Signature = oneTimeKyber.Signature!,
                        CreatedAt = _clock.GetUtcNow()
                    };

                return new PreKeyBundle
                {
                    Address = protocolAddress,
                    RegistrationId = device.RegistrationId,
                    IdentityKey = device.IdentityKey,
                    SignedPreKey = signedPreKey,
                    OneTimePreKey = oneTimeCurve,
                    KyberPreKey = kyberPreKey,
                    ServedLastResortKyberPreKey = servedLastResort,
                };
            }, ct);

            if (assembled == null)
            {
                return Result<PreKeyBundle>.Failure(SessionSetupError.InvalidKeyMaterial(
                $"Device {protocolAddress} has not published a complete set of signed prekeys."));
            }

            WarnAboutDegradation(protocolAddress, assembled);
            return Result<PreKeyBundle>.Success(assembled);
        }

        public async Task<Result<PreKeyInventory>> GetInventoryAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            if (!await _devices.ExistsAsync(address, ct))
            {
                return Result<PreKeyInventory>.Failure(SessionSetupError.DeviceNotFound(address));
            }

            return Result<PreKeyInventory>.Success(await _preKeys.CountAsync(address, ct));
        }

        public async Task<Result<PreKeyInventory>> PublishAsync(
            ProtocolAddress address, 
            PublishPreKeys publication, 
            CancellationToken ct = default)
        {
            if (!await _devices.ExistsAsync(address,ct))
            {
                return Result<PreKeyInventory>.Failure(SessionSetupError.DeviceNotFound(address));
            }

            if (KeyMaterialValidator.ValidatePublication(publication, _policyValue.MaxKeysPerUpload) is { } problem)
            {
                return Result<PreKeyInventory>.Failure(problem);
            }

            var existing = await _preKeys.CountAsync(address, ct);
            if (existing.Curve + publication.OneTimePreKeys.Count() > _policyValue.MaxPoolSize ||
                existing.Kyber + publication.OneTimeKyberPreKeys.Count() > _policyValue.MaxPoolSize)
            {
                return Result<PreKeyInventory>.Failure(SessionSetupError.PoolLimitExceeded(_policyValue.MaxPoolSize));
            }

            PreKeyInventory inventory = await _unitOfWork.ExecuteAsync(async token =>
            {
                if (publication.SignedPreKey is { } rotateCurve)
                {
                    await _preKeys.UpsertSignedPreKeyAsync(address, rotateCurve, token);
                }

                if (publication.LastResortKyberPreKey is { } rotatedKyber)
                {
                    await _preKeys.UpsertSignedPreKeyAsync(address, rotatedKyber, token);
                }

                await _preKeys.AddOneTimePreKeysAsync(address, publication.OneTimePreKeys, token);
                await _preKeys.AddOneTimePreKeysAsync(address, publication.OneTimeKyberPreKeys, token);
                await _devices.TouchAsync(address, token);

                return await _preKeys.CountAsync(address, token);
            }, ct);

            _logger.LogInformation("{Address} published keys; pools now curve={Curve}, kyber={Kyber}",
            address, inventory.Curve, inventory.Kyber);

            return Result<PreKeyInventory>.Success(inventory);
        }

        private void WarnAboutDegradation(ProtocolAddress address, PreKeyBundle bundle)
        {
            if (bundle.IsFullStrength())
            {
                return;
            }

            _logger.LogWarning(
            "Served a degraded bundle for {Address}: DH4 omitted = {NoOneTime}, last-resort Kyber key = {LastResort}",
            address, bundle.OneTimePreKey is null, bundle.ServedLastResortKyberPreKey);
        }
    }
}
