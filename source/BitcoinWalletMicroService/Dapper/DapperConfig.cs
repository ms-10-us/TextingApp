using Dapper;

namespace BitcoinWalletMicroService.Dapper
{
    public static class DapperConfig
    {
        private static readonly object Gate = new object();
        private static bool _registered;

        public static void Register()
        {
            lock(Gate)
            {
                if (_registered)
                {
                    return;
                }

                SqlMapper.AddTypeHandler(new UtcDateTimeHandler());
                SqlMapper.AddTypeHandler(new NullableUtcDateTimeHandler());

                DefaultTypeMap.MatchNamesWithUnderscores = false;

                _registered = true;
            }
        }
    }
}
