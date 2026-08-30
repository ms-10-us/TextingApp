using System.Data;

namespace BitcoinWalletMicroService.DBSqlite
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateOpenConnection();
    }
}
