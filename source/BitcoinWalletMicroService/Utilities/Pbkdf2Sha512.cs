using System.Security.Cryptography;

namespace BitcoinWalletMicroService.Utilities
{
    public class Pbkdf2Sha512
    {
        public static byte[] Derive(byte[] password, byte[] salt, int iterations, int outputBytes)
        {
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(salt);
            ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(outputBytes, 1);

            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA512,
                outputBytes);
        }
    }
}
