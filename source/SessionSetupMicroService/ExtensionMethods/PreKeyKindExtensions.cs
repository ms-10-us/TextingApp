using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.ExtensionMethods
{
    public static class PreKeyKindExtensions
    {
        public static string ToAlgorithm(this PreKeyKind kind) => kind switch
        {
            PreKeyKind.Curve => KeyAlgorithms.X25519,
            PreKeyKind.Kyber => KeyAlgorithms.Kyber1024,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unkown prekey kind.")
        };

        public static bool Matches(this PreKeyKind kind, string algorith) =>
            string.Equals(kind.ToAlgorithm(), algorith, StringComparison.Ordinal);
    }
}
