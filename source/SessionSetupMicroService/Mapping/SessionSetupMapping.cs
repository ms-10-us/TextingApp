using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Entities;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.ExtensionMethods;
using SessionSetupMicroService.Models;
using System.Security.Cryptography;

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
                SignedPreKey = request.SignedPreKey.ToModel(PreKeyKind.Curve),
                LastResortKyberPreKey = request.LastResortKyberPreKey.ToModel(PreKeyKind.Kyber),
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

        public static PreKeyBundleResponse ToResponse(this PreKeyBundle model)
        {
            return new PreKeyBundleResponse
            {
                Address = model.Address.ToString(),
                RegistrationId = model.RegistrationId.Value,
                IdentityKey = model.IdentityKey.ToDto(),
                SignedPreKey = model.SignedPreKey.ToDto(),
                OneTimePreKey = model.OneTimePreKey?.ToDto(),
                KyberPreKey = model.KyberPreKey.ToDto(),
                ServedLastResortKyberPreKey = model.ServedLastResortKyberPreKey
            }; 
        }

        public static PreKeyKind ToModel(string value) => value switch
        {
            CurveValue => PreKeyKind.Curve,
            KyberValue => PreKeyKind.Kyber,
            _ => throw new InvalidOperationException($"Unkown prekey kind stored in the database : '{value}'.")
        };

        public static SignedPreKey ToModel(this SignedPreKeyEntity entity)
        {
            PreKeyKind kind = ToModel(entity.Kind);
            return new SignedPreKey
            {
                Kind = kind,
                Id = new PreKeyId(entity.KeyId),
                PublicKey = new PublicKey
                {
                    Algorithm = kind.ToAlgorithm(),
                    Value = entity.PublicKey
                },
                Signature = entity.Signature,
                CreatedAt = entity.CreatedAt
            };
        }

        public static OneTimePreKey ToModel(this OneTimePreKeyEntity entity)
        {
            PreKeyKind kind = ToModel(entity.Kind);
            return new OneTimePreKey
            {
                Kind = kind,
                Id = new PreKeyId(entity.KeyId),
                PublicKey = new PublicKey
                {
                    Algorithm = kind.ToAlgorithm(),
                    Value = entity.PublicKey
                },
                Signature = entity.Signature,
            };
        }

        public static PreKeyInventoryResponse ToResponse(this PreKeyInventory inventory, int lowWaterMark, int maxPoolSize)
        {
            return new PreKeyInventoryResponse
            {
                OneTimePreKeys = inventory.Curve,
                OneTimeKyberPreKeys = inventory.Kyber,
                NeedsReplenishment = inventory.IsBelow(lowWaterMark),
                LowWaterMark = lowWaterMark,
                MaxPoolSize = maxPoolSize
            };            
        }

        public static PublishPreKeys ToModel(this PublishPreKeysDto dto)
        {
            return new PublishPreKeys
            {

                SignedPreKey = dto.SignedPreKey?.ToModel(PreKeyKind.Curve),
                LastResortKyberPreKey = dto.LastResortKyberPreKey?.ToModel(PreKeyKind.Kyber),
                OneTimePreKeys = (dto.OneTimePreKeys ?? []).Select(key => key.ToModel(PreKeyKind.Curve)).ToList(),
                OneTimeKyberPreKeys = (dto.OneTimeKyberPreKeys ?? []).Select(key => key.ToOneTimeModel(PreKeyKind.Kyber)).ToList()
            };
        }

        public static PublicKeyDto ToDto(this KeyPair model)
        {
            return new PublicKeyDto
            {
                Algorithm = model.Algorithm,
                Key = model.PublicKey
            };
        }

        public static SignedPreKeyDto ToDto(this GeneratedSignedPreKey model)
        {
            return new SignedPreKeyDto
            {
                KeyId = model.KeyId,
                PublicKey = model.KeyPair.ToDto(),
                Signature = model.Signature
            };
        }

        public static OneTimePreKeyDto ToDto(this GeneratedOneTimePreKey model)
        {
            return new OneTimePreKeyDto
            {
                KeyId = model.KeyId,
                PublicKey = model.KeyPair.ToDto()
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

        private static SignedPreKey ToModel(this SignedPreKeyDto dto, PreKeyKind kind)
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

        private static SignedPreKeyDto ToDto(this SignedPreKey model)
        {
            return new SignedPreKeyDto
            {
                KeyId = model.Id.Value,
                PublicKey = model.PublicKey.ToDto(),
                Signature = model.Signature
            };
        }

        private static OneTimePreKeyDto ToDto(this OneTimePreKey model)
        {
            return new OneTimePreKeyDto
            {
                KeyId = model.Id.Value,
                PublicKey = model.PublicKey.ToDto(),
            };

        }

    }
}
