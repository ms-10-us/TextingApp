using SessionSetupMicroService.Enums;
using SessionSetupMicroService.ExtensionMethods;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.OperationResult;

namespace SessionSetupMicroService.Validators
{
    public static class KeyMaterialValidator
    {

        public static SessionSetupError? ValidateRegistration(RegisterDeviceModel registration, int maxKeysPerUpload)
        {
            if (string.IsNullOrWhiteSpace(registration.DisplayName))
            {
                return SessionSetupError.InvalidKeyMaterial("A device name is required.");
            }

            if (registration.DisplayName.Length > 128)
            {
                return SessionSetupError.InvalidKeyMaterial("Device names are limited to 128 characters.");
            }

            if (ValidateIdentityKey(registration.IdentityKey) is { } identityProblem)
            {
                return identityProblem;
            }

            if (ValidateSignedPreKey(registration.SignedPreKey, PreKeyKind.Curve) is { } curveProblem)
            {
                return curveProblem;
            }

            if (ValidateSignedPreKey(registration.LastResortKyberPreKey, PreKeyKind.Kyber) is { } kyberProblem)
            {
                return kyberProblem;
            }

            return ValidateOneTimeBatches(registration.OneTimePreKeys, registration.OneTimeKyberPreKeys, maxKeysPerUpload);
        }

        public static SessionSetupError? ValidatePublication(PublishPreKeys publication, int maxKeysPerUpload)
        {
            if (publication.IsEmpty)
            {
                return SessionSetupError.NothingToPublish();
            }

            if (publication.SignedPreKey is { } signed && ValidateSignedPreKey(signed, PreKeyKind.Curve) is { } curveProblem)
            {
                return curveProblem;
            }

            if (publication.LastResortKyberPreKey is { } lastResort 
                && ValidateSignedPreKey(lastResort, PreKeyKind.Kyber) is { } kyberProblem)
                return kyberProblem;

            return ValidateOneTimeBatches(publication.OneTimePreKeys, publication.OneTimeKyberPreKeys, maxKeysPerUpload);
        }

        private static SessionSetupError? ValidateIdentityKey(PublicKey key)
        {
            if (!string.Equals(key.Algorithm, KeyAlgorithms.Ed25519, StringComparison.Ordinal))
                return SessionSetupError.InvalidKeyMaterial(
                    $"Identity keys must be {KeyAlgorithms.Ed25519}, not '{key.Algorithm}'.");

            return ValidateLength(key);
        }

        private static SessionSetupError? ValidateLength(PublicKey key)
        {
            int? expected = KeySizes.ExpectedPublicKeySize(key.Algorithm);
            if (expected == null)
            {
                return SessionSetupError.InvalidKeyMaterial($"Unknown key algorithm '{key.Algorithm}'.");
            }

            return key.Length == expected
                ? null
                : SessionSetupError.InvalidKeyMaterial(
                    $"A {key.Algorithm} public key must be {expected} bytes, got {key.Length}.");
        }

        private static SessionSetupError? ValidateSignedPreKey(SignedPreKey key, PreKeyKind expectedKind)
        {
            if (key.Kind != expectedKind)
            {
                return SessionSetupError.InvalidKeyMaterial($"Expected a {expectedKind} prekey, got {key.Kind}.");
            }

            if (!expectedKind.Matches(key.PublicKey.Algorithm))
            {
                return SessionSetupError.InvalidKeyMaterial($"A {expectedKind} prekey must use {expectedKind.ToAlgorithm()}," +
                    $"not '{key.PublicKey.Algorithm}'.");
            }

            if (ValidateLength(key.PublicKey) is { } lengthProblem)
            {
                return lengthProblem;
            }

            if (key.Signature.Length != KeySizes.Ed25519Signature)
            {
                return SessionSetupError.InvalidKeyMaterial("" +
                    $"Prekey signatures must be {KeySizes.Ed25519Signature} bytes, got {key.Signature.Length}.");
            }

            if (key.Id.Value < 0)
            {
                return SessionSetupError.InvalidKeyMaterial("Prekey ids must not be negative.");
            }

            return null;
        }

        private static SessionSetupError? ValidateOneTimeBatches(IEnumerable<OneTimePreKey> curve, IEnumerable<OneTimePreKey> kyber,
            int maxKeysPerUpload)
        {
            if (curve.Count() > maxKeysPerUpload || kyber.Count() > maxKeysPerUpload)
            {
                return SessionSetupError.InvalidKeyMaterial($"At most {maxKeysPerUpload} one-time keys may be published at once.");
            }

            if (ValidateOneTimeBatch(curve, PreKeyKind.Curve) is { } curveProblem)
            {
                return curveProblem;
            }

            return ValidateOneTimeBatch(kyber, PreKeyKind.Kyber);
        }

        private static SessionSetupError? ValidateOneTimeBatch(IEnumerable<OneTimePreKey> keys, PreKeyKind expectedKind)
        {
            if (keys.Select(k => k.Id.Value).Distinct().Count() != keys.Count())
            {
                return SessionSetupError.InvalidKeyMaterial("One-time prekey ids must be unique within an upload.");
            }

            foreach (var key in keys)
            {
                if (key.Kind != expectedKind)
                {
                    return SessionSetupError.InvalidKeyMaterial($"Expected a {expectedKind} one-time prekey, got {key.Kind}.");
                }

                if (!expectedKind.Matches(key.PublicKey.Algorithm))
                {
                    return SessionSetupError.InvalidKeyMaterial($"" +
                        $"A {expectedKind} one-time prekey must use {expectedKind.ToAlgorithm()}, not '{key.PublicKey.Algorithm}'.");
                }

                if (key.Id.Value < 0)
                {
                    return SessionSetupError.InvalidKeyMaterial("PreKey idsmust not be negative.");
                }

                if (expectedKind == PreKeyKind.Kyber && !key.IsSigned)
                {
                    return SessionSetupError.InvalidKeyMaterial("One-time Kyber prekeys must carry a signature.");
                }                
            }

            return null;
        }
    }
}
