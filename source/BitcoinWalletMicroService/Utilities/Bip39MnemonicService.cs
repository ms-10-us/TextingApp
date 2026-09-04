using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BitcoinWalletMicroService.Utilities
{
    public class Bip39MnemonicService : IMnemonicService
    {
        private const int PbkdfIterations = 2048;
        private const int SeedLengthBytes = 64;
        private const string SaltPerfix = "mnemonic";

        public string Fingerprint(string mnemonic, string passphrase)
        {
            byte[] seed = ToSeed(mnemonic, passphrase);

            try
            {
                using (var sha = SHA256.Create())
                {
                    byte[] prefix = Encoding.UTF8.GetBytes("wallet-fingerprint-v1|");
                    var buffer = new byte[prefix.Length + seed.Length];
                    Buffer.BlockCopy(prefix, 0, buffer, 0, prefix.Length);
                    Buffer.BlockCopy(seed, 0, buffer, prefix.Length, seed.Length);

                    byte[] digest = sha.ComputeHash(buffer);
                    Array.Clear(buffer, 0, buffer.Length);
                    return ToHex(digest);
                }
            }
            finally 
            {
                Array.Clear(seed, 0, seed.Length);
            }
        }

        public MnemonicResult Generate(MnemonicStrength strength)
        {
            int entropyBits = (int)strength;
            ValidateEntropyBits(entropyBits);

            var entropy = new byte[entropyBits / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(entropy);  
            }

            try
            {
                string[] words = EntropyToWords(entropy);
                return new MnemonicResult
                {
                    Mnemonic = string.Join(" ", words),
                    EntropyHex = ToHex(entropy),
                    WordCount = words.Length
                };
            }
            finally
            {
                Array.Clear(entropy, 0, entropy.Length);
            }
        }

        public bool IsValid(string mnemonic)
        {
            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                return false;
            }

            string[] words = Normalize(mnemonic)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
            if (words.Length < 12 || words.Length > 24 || words.Length % 3 != 0)
            {
                return false;
            }

            int totalBits = words.Length * 11;
            int checksumBits = totalBits / 33;
            int entropyBits = totalBits - checksumBits;
            if (entropyBits % 8 != 0)
            {
                return false;
            }

            var bits = new bool[totalBits];
            for (int w = 0; w < words.Length; w++)
            {
                int index;
                if (!EnglishWordList.TryGetIndex(words[w], out index))
                {
                    return false;
                }
                
                for (int b = 0; b < 11; b++)
                {
                    bits[w * 11 + b] = ((index >> (10 - b)) & 1) != 0;
                }
            }

            var entropy = new byte[entropyBits / 8];
            for (int i = 0; i < entropyBits; i++)
            {
                if (bits[i])
                {
                    entropy[i / 8] |= (byte)(1 << (7 - (i % 8)));
                }
            }

            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(entropy);
            }

            for (int i = 0; i < checksumBits; i++)
            {
                bool expected = ((hash[i / 8] >> (7 - (i % 8))) & 1) != 0;
                if (bits[ entropyBits + i] != expected)
                {
                    Array.Clear(entropy, 0, entropy.Length);
                    return false;
                }
            }

            Array.Clear(entropy, 0, entropy.Length);
            return true;
        }

        public byte[] ToSeed(string mnemonic, string passphrase)
        {
            if (string.IsNullOrEmpty(mnemonic))
            {
                throw new ArgumentException("Mnemonic is required.", "mnemonic");
            }

            string normalizedMnemonic = Normalize(mnemonic);

            string normalizedSalt = SaltPerfix + (passphrase ?? string.Empty).Normalize(NormalizationForm.FormKD);

            byte[] passwordBytes = Encoding.UTF8.GetBytes(normalizedMnemonic);
            byte[] saltBytes = Encoding.UTF8.GetBytes(normalizedSalt);

            try
            {
                return Pbkdf2Sha512.Derive(passwordBytes, saltBytes, PbkdfIterations, SeedLengthBytes);
            }
            finally
            {
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                Array.Clear(saltBytes, 0, saltBytes.Length);
            }
        }

        private static void ValidateEntropyBits(int entropyBits)
        {
            if (entropyBits < 128 || entropyBits > 256 || entropyBits % 32 != 0)
            {
                throw new ArgumentOutOfRangeException(
                    "strength",
                    "Entropy must be 128 - 256 bits in multiples of 32");
            }
        }

        private static string[] EntropyToWords(byte[] entropy)
        {
            int entropyBits = entropy.Length * 8;
            int checksumBits = entropyBits / 32;

            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(entropy);
            }

            var bits = new bool[entropyBits + checksumBits];


            for (int i = 0; i < entropyBits; i++)
            {
                bits[i] = ((entropy[i / 8] >> (7 - (i % 8))) & 1) != 0;
            }

            for (int i = 0; i < checksumBits; i++)
            {
                bits[entropyBits + i] = ((hash[i / 8] >> (7 - (i % 8))) & 1) != 0;
            }

            int wordCount = bits.Length / 11;
            var words = new string[wordCount];
            var wordList = EnglishWordList.Words;

            for (int w = 0; w < wordCount; w++)
            {
                int index = 0;
                for (int b = 0; b < 11; b++)
                {
                    index = (index << 1) | (bits[w * 11 + b] ? 1 : 0);
                }
                words[w] = wordList[index];
            }

            Array.Clear(bits, 0, bits.Length);
            return words;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static string Normalize(string value)
        {
            string normalized = (value ?? string.Empty)
                .Normalize(NormalizationForm.FormKD)
                .Trim()
                .ToLower(CultureInfo.InvariantCulture);

            return string.Join(" ", normalized.Split(
                new[] { ' ', '\t', '\n', '\u3000' },
                StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
