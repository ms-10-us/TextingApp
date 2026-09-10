using System.Data.Common;

namespace SessionSetupMicroService.ExtensionMethods
{
    public static class DbExtensions
    {
        public static DbCommand Bind(this DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
            return command;
        }

        public static byte[] GetBytes(this DbDataReader reader, string column) =>
            reader.GetFieldValue<byte[]>(reader.GetOrdinal(column));

        public static byte[]? GetNullableBytes(this DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<byte[]>(ordinal);
        }

        public static string GetText(this DbDataReader reader, string column) =>
            reader.GetString(reader.GetOrdinal(column));

        public static int GetInt(this DbDataReader reader, string column) =>
            reader.GetInt32(reader.GetOrdinal(column));

        public static long GetLong(this DbDataReader reader, string column) =>
            reader.GetInt64(reader.GetOrdinal(column));

        public static Guid GetGuid(this DbDataReader reader, string column) =>
            reader.GetGuid(reader.GetOrdinal(column));

        public static DateTimeOffset GetTimestamp(this DbDataReader reader, string column) =>
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(column));
    }
}
