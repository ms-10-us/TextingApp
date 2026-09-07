namespace BitcoinWalletMicroService.Models
{
    public class SendModel
    {

        public required string WalletId { get; set; }

        public required string ToAddress { get; set; }

        public string? Passphrase { get; set; }

        public required long AmountSats { get; set; }

        public decimal? FeeSats { get; set; }

        public bool SweepAll { get; set; }

        public bool DryRun { get; set; }

        public string? IdempotencyKey { get; set; }
    }
}
