using NSubstitute.Exceptions;
using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SessionSetupMicroServiceUnitTest.TestDoubles
{
    public static class Any
    {
        public const int CurveKeyBytes = 32;
        public const int KyberKeyBytes = 1568;
        public const int SignatureBytes = 64;

        public static AccountId Account()
        {
            return AccountId.New();
        }

        public static ProtocolAddress Address(int deviceId = 1)
        {
            return new ProtocolAddress(Account(), new DeviceId(deviceId));
        }

        public static ProtocolAddress AddressOn(AccountId account, int deviceId)
        {
            return new ProtocolAddress(account, new DeviceId(deviceId));
        }

        public static byte[] Bytes(int length, byte seed = 1)
        {
            return Enumerable.Range(0, length).Select(index => (byte)(index + seed)).ToArray();
        }

        public static PublicKey IdentityKey(byte seed = 1)
        {
            return new PublicKey
            {
                Algorithm = KeyAlgorithms.Ed25519,
                Value = Bytes(CurveKeyBytes, seed)
            };
        }

        public static PublicKey CurveKey(byte seed = 2)
        {
            return new PublicKey
            {
                Algorithm = KeyAlgorithms.X25519,
                Value = Bytes(CurveKeyBytes, seed)
            };
        }

        public static PublicKey KyberKey(byte seed = 3)
        {
            return new PublicKey
            {
                Algorithm = KeyAlgorithms.Kyber1024,
                Value = Bytes(KyberKeyBytes, seed)
            };
        }

        public static SignedPreKey SignedCurvePreKey(long keyId = 1)
        {
            return new SignedPreKey
            {
                Kind = PreKeyKind.Curve,
                Id = new PreKeyId(keyId),
                PublicKey = CurveKey(),
                Signature = Bytes(SignatureBytes),
                CreatedAt = FixedClock.Default
            };
        }

        public static SignedPreKey LastResortKyberPreKey(long keyId = 1)
        {
            return new SignedPreKey
            {
                Kind = PreKeyKind.Kyber,
                Id = new PreKeyId(keyId),
                PublicKey = KyberKey(),
                Signature = Bytes(SignatureBytes),
                CreatedAt = FixedClock.Default
            };
        }

        public static OneTimePreKey OneTimeCurve(long keyId)
        {
            return new OneTimePreKey
            {
                Kind = PreKeyKind.Curve,
                Id = new PreKeyId(keyId),
                PublicKey = CurveKey((byte)keyId),
                Signature = null
            };
        }

        public static OneTimePreKey OneTimeKyber(long keyId)
        {
            return new OneTimePreKey
            {
                Kind = PreKeyKind.Kyber,
                Id = new PreKeyId(keyId),
                PublicKey = KyberKey((byte)keyId),
                Signature = Bytes(SignatureBytes)
            };
        }

        public static Device Device(ProtocolAddress address, int registrationId = 4242)
        {
            return new Device
            {
                Address = address,
                DisplayName = "Test Device",
                RegistrationId = new RegistrationId(registrationId),
                IdentityKey = IdentityKey(),
                RegisteredAt = FixedClock.Default,
                LastSeen = FixedClock.Default
            };
        }

        public static RegisterDeviceModel Registration(
            int curveCount = 5,
            int kyberCount = 3,
            string displayName = "Test device",
            int registrationId = 4242)
        {
            return new RegisterDeviceModel
            {
                DisplayName = displayName,
                RegistrationId = new RegistrationId(registrationId),
                IdentityKey = IdentityKey(),
                SignedPreKey = SignedCurvePreKey(),
                LastResortKyberPreKey = LastResortKyberPreKey(),
                OneTimePreKeys = Enumerable.Range(1, curveCount).Select(id => OneTimeCurve(id)).ToList(),
                OneTimeKyberPreKeys = Enumerable.Range(1, kyberCount).Select(id => OneTimeKyber(id)).ToList()
            };
        }

        public static PublishPreKeys Publication(int curveCount = 2, int kyberCount = 2)
        {
            return new PublishPreKeys
            {
                SignedPreKey = null,
                LastResortKyberPreKey = null,
                OneTimePreKeys = Enumerable.Range(1, curveCount).Select(id => OneTimeCurve(id)).ToList(),
                OneTimeKyberPreKeys = Enumerable.Range(1, kyberCount).Select(id => OneTimeKyber(id)).ToList()
            };
        }

        public static PublicKeyDto Dto(PublicKey key)
        {
            return new PublicKeyDto
            {
                Algorithm = key.Algorithm,
                Key = key.Value
            };
        }

        public static RegisterDeviceRequest Request(int registrationId = 4242)
        {
            return new RegisterDeviceRequest
            {
                DeviceName = "Test device",
                RegistrationId = registrationId,
                IdentityKey = Dto(IdentityKey()),
                SignedPreKey = new SignedPreKeyDto
                {
                    KeyId = 1,
                    PublicKey = Dto(CurveKey()),
                    Signature = Bytes(SignatureBytes)
                },
                LastResortKyberPreKey = new SignedPreKeyDto
                {
                    KeyId = 1,
                    PublicKey = Dto(KyberKey()),
                    Signature = Bytes(SignatureBytes)
                },
                OneTimePreKeys = new List<OneTimePreKeyDto>
                {
                new() { KeyId = 1, PublicKey = Dto(CurveKey()) }
                },
                OneTimeKyberPreKeys = new List<SignedPreKeyDto>
                {
                new() { KeyId = 1, PublicKey = Dto(KyberKey()), Signature = Bytes(SignatureBytes) }
                }
            };
        }
    }
}
