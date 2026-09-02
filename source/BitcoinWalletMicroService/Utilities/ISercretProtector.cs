namespace BitcoinWalletMicroService.Utilities
{
    public interface ISercretProtector
    {
        byte[] Protect(string plaintext);
        string Unprotect(byte[] ciphertext);
    }
}
