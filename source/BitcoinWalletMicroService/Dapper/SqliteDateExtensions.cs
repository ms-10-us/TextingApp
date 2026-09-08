using System.Globalization;

namespace BitcoinWalletMicroService.Dapper
{
    public static class SqliteDateExtensions
    {
        public static string ToSqliteUtc(this DateTime value) =>
            (value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime())
                .ToString("o", CultureInfo.InvariantCulture);

        public static string? ToSqliteUtc(this DateTime? value) =>
            value?.ToSqliteUtc();
    }
}
