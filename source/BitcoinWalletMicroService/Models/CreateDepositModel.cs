using System.ComponentModel.DataAnnotations;

namespace BitcoinWalletMicroService.Models
{
    public class CreateDepositModel
    {
        public required string WalletId { get; set; }

        public long? ExpectedSats { get; set; }

        public string? Label { get; set; }

        public int ExpiryMinutes { get; set; } = 1440;
    }
}
