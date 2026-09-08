using System.ComponentModel.DataAnnotations;

namespace BitcoinWalletMicroService.Dtos
{
    public class CreateDepositDto
    {
        [Range(294, 2_100_000_000_000_000, ErrorMessage = "Expected amount must be between 294 sats and 21M BTC supply cap.")]
        public long? ExpectedSats {  get; set; }

        [StringLength(100)]
        public string? Label { get; set; }

        [Range(5, 43200, ErrorMessage = "Expiry must be between 5 minuts and 30 days.")]
        public int ExpiryMinutes { get; set; } = 1440;
    }
}
