using System.Security.Cryptography;
using System.Text;

namespace BitcoinWalletMicroService.Utilities
{
    public class AesGcmSecretProtector : ISercretProtector, IDisposable
    {
        private const byte FormatVersion = 1;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        private readonly AesGcm _aesGcm;

        public AesGcmSecretProtector(string secretEntropy)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretEntropy);

            if (secretEntropy.Length < 32)
            {
                throw new ArgumentException(
                    "Wallet:SecretEntropy must be at least 32 characters.", nameof(secretEntropy));
            }

            byte[] key = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: Encoding.UTF8.GetBytes(secretEntropy),
                outputLength: KeySize,
                salt: null,
                info: Encoding.UTF8.GetBytes("BitcoinWallet.MnemonicProtection.v1"));

            try
            {
                _aesGcm = new AesGcm(key, TagSize);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

        }    

        public byte[] Protect(string plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);

            try
            {
                byte[] output = new byte[1 + NonceSize + plainBytes.Length + TagSize];
                output[0] = FormatVersion;

                Span<byte> nonce = output.AsSpan(1, NonceSize);
                Span<byte> ciphertext = output.AsSpan(1 + NonceSize, plainBytes.Length);
                Span<byte> tag = output.AsSpan(1 + NonceSize + plainBytes.Length, TagSize);

                RandomNumberGenerator.Fill(nonce);

                _aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);

                return output;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        public string Unprotect(byte[] ciphertext)
        {
            ArgumentNullException.ThrowIfNull(ciphertext);


            if (ciphertext.Length < 1 + NonceSize + TagSize)
            {
                throw new CryptographicException("Ciphertext is too short to be valid");
            }

            if (ciphertext[0] != FormatVersion)
            {
                throw new CryptographicException($"Unsupported ciphertext format version {ciphertext[0]}.");
            }

            int plainLength = ciphertext.Length - 1 - NonceSize - TagSize;

            ReadOnlySpan<byte> nonce = ciphertext.AsSpan(1, NonceSize);
            ReadOnlySpan<byte> payload = ciphertext.AsSpan(1 + NonceSize, plainLength);
            ReadOnlySpan<byte> tag = ciphertext.AsSpan(1 + NonceSize + plainLength, TagSize);

            byte[] plainBytes = new byte[plainLength];

            try
            {
                _aesGcm.Decrypt(nonce, payload, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        public void Dispose() => _aesGcm.Dispose();
    }
}
