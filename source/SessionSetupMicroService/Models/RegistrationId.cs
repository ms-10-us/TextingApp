using System.Diagnostics.CodeAnalysis;

namespace SessionSetupMicroService.Models
{
    public readonly record struct RegistrationId
    {
        public const int MaxValue = 0x3FFF;

        public int Value { get; }

        public RegistrationId(int value)
        {
            if (value is < 0 or > MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Registration ids are 14-bit.");
            }

            Value = value;
        }

        public static RegistrationId Generate() =>
            new(System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, MaxValue + 1));

        public static bool TryCreate(int value, [NotNullWhen(true)] out RegistrationId? registrationId)
        {
            if (value is < 0 or > MaxValue)
            {
                registrationId = null;
                return false;
            }

            registrationId = new RegistrationId(value);
            return true;
        }

        public override string ToString() => Value.ToString();
    }
}
