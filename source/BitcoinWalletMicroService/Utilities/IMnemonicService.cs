using BitcoinWalletMicroService.Enums;
using BitcoinWalletMicroService.Models;

namespace BitcoinWalletMicroService.Utilities
{
    public interface IMnemonicService
    {
        MnemonicResult Generate(MnemonicStrength strength);

        bool IsValid(string mnemonic);

        byte[] ToSeed(string mnemonic, string passphrase);

        string Fingerprint(string mnemonic, string passphrase);
    }
}
