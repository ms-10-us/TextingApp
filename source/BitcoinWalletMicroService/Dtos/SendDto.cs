using System.ComponentModel.DataAnnotations;

namespace BitcoinWalletMicroService.Dtos
{
    public class SendDto : IValidatableObject
    {
        [Required(ErrorMessage = "Destination address is required.")]
        [StringLength(90, MinimumLength = 26, ErrorMessage = "Address length is not valid for any Bitcoin address format.")]
        public string ToAddress { get; set; } = string.Empty;

        [Range(294, 2_100_000_000_000_000, ErrorMessage = "Amount must be between 294 sats (dust limit) and the 21M BTC supply cap.")]
        public long AmountSats { get; set; }

        public string? Passphrase { get; set; }

        [Range(1.0, 1000.0, ErrorMessage = "Fee rate must be between 1 and 1000 sat/vB.")]
        public decimal? FeeRateSatsPerVByte { get; set; }

        public bool SweepAll { get; set; }

        public bool DryRun { get; set; } = true;

        [StringLength(100, MinimumLength = 8)]
        public string? IdempotencyKey { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!SweepAll && AmountSats < 294)
            {
                yield return new ValidationResult(
                    "AmountSats is required and must be at least 294 sats unles SweepAll is true.",
                    [nameof(AmountSats)]);
            }

            if (SweepAll && AmountSats > 0)
            {
                yield return new ValidationResult(
                    "AmountSats must not be set when SweepAll is true - the whole balance is sent.",
                    [nameof(AmountSats), nameof(SweepAll)]);
            }

            if (!DryRun && string.IsNullOrWhiteSpace(IdempotencyKey))
            {
                yield return new ValidationResult(
                    "IdempotencyKey is required when DryRun is false, so a retried request cannot broadcast twice.",
                    [nameof(IdempotencyKey)]);
            }
        }
    }
}
