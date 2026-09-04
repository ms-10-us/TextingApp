using System.Globalization;
using System.Text;

namespace BitcoinWalletMicroService.Utilities
{
    public class BipUriBuilder
    {
        public static string Build(
            string address,
            decimal? amountBtc = null,
            string? label = null,
            string? message = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(address);

            if (amountBtc <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountBtc), "Amount cannot be negative");
            }

            bool hasParameters = amountBtc.HasValue
                || !string.IsNullOrWhiteSpace(label)
                || !string.IsNullOrWhiteSpace(message);

            bool isBech32 = address.StartsWith("bc1", StringComparison.OrdinalIgnoreCase)
                         || address.StartsWith("tb1", StringComparison.OrdinalIgnoreCase)
                         || address.StartsWith("bcrt1", StringComparison.OrdinalIgnoreCase);

            if (isBech32 && !hasParameters)
            {
                return $"BITCOIN: {address.ToUpperInvariant()}";
            }

            var uri = new StringBuilder("bitcoin:").Append(address);

            if (!hasParameters)
            {
                return uri.ToString();
            }

            char seperator = '?';

            if (amountBtc.HasValue)
            {
                uri.Append(seperator)
                    .Append("amount=")
                    .Append(amountBtc.Value.ToString("0.########", CultureInfo.InvariantCulture));

                seperator = '&';
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                uri.Append(seperator).Append("label=").Append(Uri.EscapeDataString(label));
                seperator = '&';
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                uri.Append(seperator).Append("message=").Append(Uri.EscapeDataString(message));
            }

            return uri.ToString();
        }
    }
}