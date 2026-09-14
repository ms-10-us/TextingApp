using SessionSetupMicroService.Dtos;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.Protocol.SignalFFI;
using System.Security.Cryptography;

namespace SessionSetupMicroService.Protocol
{
    public class LibsignalSessionSetupProtocol : ISessionSetupProtocol
    {
        public KeyPair GenerateCurveKeyPair()
        {
            SignalFfi.Check(NativeMethods.signal_privatekey_generate(out var rawPrivateKey));
            using var privateKey = SignalFfi.Own<PrivateKeyHandle>(rawPrivateKey);

            SignalFfi.Check(NativeMethods.signal_privatekey_get_public_key(out var rawPublicKey, privateKey.Borrow()));
            using var publicKey = SignalFfi.Own<PublicKeyHandle>(rawPublicKey);

            SignalFfi.Check(NativeMethods.signal_privatekey_serialize(out var privateBuffer, privateKey.Borrow()));
            SignalFfi.Check(NativeMethods.signal_privatekey_serialize(out var publicBuffer, publicKey.Borrow()));

            return new KeyPair
            {
                Algorithm = KeyAlgorithms.X25519,
                PublicKey = SignalFfi.Consume(publicBuffer),
                PrivateKey = SignalFfi.Consume(privateBuffer)
            };
        }

        public static int GenerateRegistrationId()
        {
            return RandomNumberGenerator.GetInt32(1, 0x3FFF + 1);
        }

        public GeneratedSignedPreKey GenerateDeviceKeys(int oneTimePreKeyCount) => throw NotYet(
            nameof(GenerateDeviceKeys),
            "signal_kyber_key_pair_generate, signal_kyber_key_pair_get_public_key, signal_privatekey_sign");
        

        public PqxdhResult InitiateSession(GeneratedDeviceKeys initiator, PreKeyBundleResponse bundle) => throw NotYet(
            nameof(InitiateSession),
            "signal_pre_key_bundle_new, signal_process_prekey_bundle");

        public bool VerifyBundle(PreKeyBundleResponse bundle) => throw NotYet(
            nameof(VerifyBundle),
            "signal_publickey_verify");

        private static NotImplementedException NotYet(string member, string symbols) => new(
        $"{member} is not implemented yet. Generate the interop from signal_ffi.h " +
        $"(build/build-native.sh, then build/generate-interop.sh) and bind: {symbols}.");
    }
}
