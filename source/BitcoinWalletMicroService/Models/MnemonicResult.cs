namespace BitcoinWalletMicroService.Models
{
    public class MnemonicResult
    {
        public string Mnemonic { get; set; } = string.Empty;
        public string EntropyHex { get; set; } = string.Empty;
        public int WordCount { get; set; }
    }
}
