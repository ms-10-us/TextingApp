using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Mapping
{
    public static class SessionSetupMapping
    {
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

















    }
}
