using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Utilities
{
    public class MnemonicService : IMnemonicService
    {
        private const int PbkdfIterations = 2048;
        private const int SeedLengthBytes = 64;
        private const string SaltPerfix = "mnemonic";

        public string Fingerprint(string mnemonic, string passphrase)
        {
            throw new NotImplementedException();
        }

        public MnemonicResult Generate(MnemonicStrength strength)
        {
            throw new NotImplementedException();
        }

        public bool IsValid(string mnemonic)
        {
            throw new NotImplementedException();
        }

        public byte[] ToSeed(string mnemonic, string passphrase)
        {
            throw new NotImplementedException();
        }
    }
}
