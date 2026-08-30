namespace BitcoinWalletMicroService.Models
{
    public class AccountKeys
    {
        public string AccountExtendedPublicKey { get; set; } = string.Empty;

        public string AccountDerivationPath { get; set; } = string.Empty;
    }
}
