using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Entities;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Mapping
{
    public static class SessionSetupMapping
    {
        private const string CurveValue = "curve";
        private const string KyberValue = "kyber";


        public static RegisterDeviceModel? ToModel(this RegisterDeviceRequest request)
        {
            if (!RegistrationId.TryCreate(request.RegistrationId, out var registrationId))
            {
                return null;
            }

            return new RegisterDeviceModel
            {

                DisplayName = request.DeviceName,
                RegistrationId = registrationId.Value,
                IdentityKey = request.IdentityKey.ToModel(),
                SignedPreKey = request.SignedPreKey.ToMdodel(PreKeyKind.Curve),
                LastResortKyberPreKey = request.LastResortKyberPreKey.ToMdodel(PreKeyKind.Kyber),
                OneTimePreKeys = request.OneTimePreKeys.Select(key => key.ToModel(PreKeyKind.Curve)).ToList(),
                OneTimeKyberPreKeys = request.OneTimeKyberPreKeys.Select(key => key.ToOneTimeModel(PreKeyKind.Kyber)).ToList()
            };
        }

        public static RegisterDeviceResponse ToResponse(this DeviceRegistrationResult result)
        {
            return new RegisterDeviceResponse
            {
                AccountId = result.Device.Address.Account.Value,
                DeviceId = result.Device.Address.Device.Value,
                Address = result.Device.Address.ToString(),
                DeviceCredential = result.DeviceCredentials,
                RegisteredAt = result.Device.RegisteredAt
            };
        }

        public static DeviceResponse ToResponse(this Device device)
        {
            return new DeviceResponse
            {
                DeviceId = device.Address.Device.Value,
                Address = device.Address.ToString(),
                DeviceName = device.DisplayName,
                RegistrationId = device.RegistrationId.Value,
                IdentityDto = device.IdentityKey.ToDto(),
                RegisteredAt = device.RegisteredAt,
                LastSeenAt = device.LastSeen
            };
        }

        public static string ToEntity(this PreKeyKind kind) => kind switch
        {
            PreKeyKind.Curve => CurveValue,
            PreKeyKind.Kyber => KyberValue,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unkown prekey kind.")
        };

        public static Device ToModel(this DeviceEntity entity)
        {
            return new Device
            {
                Address = new ProtocolAddress(new AccountId(entity.AccountId), new DeviceId(entity.DeviceId)),
                DisplayName = entity.DisplayName,
                RegistrationId = new RegistrationId(entity.RegistrationId),
                IdentityKey = new PublicKey
                {
                    Algorithm = entity.IdentityAlgorithm,
                    Value = entity.IdentityKey,
                },
                RegisteredAt = entity.RegisteredAt,
                LastSeen = entity.LastSeenAt
            };
        }

        private static PublicKey ToModel(this  PublicKeyDto dto)
        {
            return new PublicKey
            {
                Algorithm = dto.Algorithm,
                Value = dto.Key
            };
        }

        private static SignedPreKey ToMdodel(this SignedPreKeyDto dto, PreKeyKind kind)
        {
            return new SignedPreKey
            {
                Kind = kind,
                Id = new PreKeyId(dto.KeyId),
                PublicKey = dto.PublicKey.ToModel(),
                Signature = dto.Signature,
                CreatedAt = DateTimeOffset.MinValue,
            };
        }

        private static OneTimePreKey ToModel(this OneTimePreKeyDto dto, PreKeyKind kind)
        {
            return new OneTimePreKey
            {
                Kind = kind,
                Id = new PreKeyId(dto.KeyId),
                PublicKey = dto.PublicKey.ToModel(),
                Signature = null
            };
        }

        private static OneTimePreKey ToOneTimeModel(this SignedPreKeyDto dto, PreKeyKind kind)
        {
            return new OneTimePreKey
            {
                Kind = kind,
                Id = new PreKeyId(dto.KeyId),
                PublicKey = dto.PublicKey.ToModel(),
                Signature = dto.Signature
            };
        }

        private static PublicKeyDto ToDto(this PublicKey model)
        {
            return new PublicKeyDto
            {
                Algorithm = model.Algorithm,
                Key = model.Value
            };
        }
    }
}
