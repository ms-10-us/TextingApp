namespace BitcoinWalletMicroService.Enums
{
    public enum DepositStatus
    {
        Pending = 0,
        Detected = 1,
        Underpaid = 2,
        Confirmed = 3,
        Expired = 4
    }
}
