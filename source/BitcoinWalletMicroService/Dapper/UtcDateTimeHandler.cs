using Dapper;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;

namespace BitcoinWalletMicroService.Dapper
{
    public sealed class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override DateTime Parse(object value)
        {
            if (value == null || value is DBNull)
            {
                throw new DataException("Cannot convert NULL to DateTime.");
            }

            var asDateTime = value as DateTime?;
            if (asDateTime.HasValue)
            {
                return DateTime.SpecifyKind(asDateTime.Value, DateTimeKind.Utc);
            }

            return DateTime.Parse(
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal);

        }

        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.Kind == DateTimeKind.Utc
                ? value.ToString("o", CultureInfo.InvariantCulture)
                : value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
