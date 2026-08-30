using Dapper;
using System.Data;

namespace BitcoinWalletMicroService.Dapper
{
    public sealed class NullableUtcDateTimeHandler : SqlMapper.TypeHandler<DateTime?>
    {
        private static readonly UtcDateTimeHandler Inner = new UtcDateTimeHandler();

        public override DateTime? Parse(object value)
        {
            if (value == null || value is DBNull)
            {
                return null;
            }

            return Inner.Parse(value);
        }

        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            if (!value.HasValue)
            {
                parameter.DbType = DbType.String;
                parameter.Value = DBNull.Value;
                return;
            }

            Inner.SetValue(parameter, value.Value);
        }
    }
}
