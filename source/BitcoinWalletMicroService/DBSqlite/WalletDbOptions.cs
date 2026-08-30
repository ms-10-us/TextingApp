namespace BitcoinWalletMicroService.DBSqlite
{
    public class WalletDbOptions
    {
        public string DbPath { get; set; } = "/data/wallets.db";
        public int BusyTimeoutMs { get; set; } = 5000; 
    }
}
