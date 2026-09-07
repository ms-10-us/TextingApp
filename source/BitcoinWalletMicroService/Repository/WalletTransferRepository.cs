using BitcoinWalletMicroService.DBSqlite;
using BitcoinWalletMicroService.Entities;
using Dapper;
using System.Data;

namespace BitcoinWalletMicroService.Repository
{
    public class WalletTransferRepository : IWalletTransferRepository
    {
        private const string SendTransactionColumns =
            "Id, TxId, WalletId, IdempotencyKey, ToAddress, AmountSats, FeeSats, " +
            "VirtualSizeBytes, InputCount, ChangeAddress, ChangeSats, " +
            "RawTransactionHex, BroadcastUtc";

        private readonly IDbConnectionFactory _connectionFactory = default!;

        public WalletTransferRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<SendTransactionEntity>> GetSendTransactionAsync(string walletId, CancellationToken ct)
        {

            const string sql = "SELECT " + SendTransactionColumns + @"
FROM   SentTransactions
WHERE  WalletId = @WalletId
ORDER BY BroadcastUtc DESC;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                var rows = await connection.QueryAsync<SendTransactionEntity>(
                    new CommandDefinition(sql, new { WalletId = walletId }, cancellationToken: ct))
                    .ConfigureAwait(false);

                return rows.ToList();
            }
        }

        public async Task<SendTransactionEntity?> GetSendTransactionByIdempotencyKeyAsync(string walletId, string idempotencyKey, CancellationToken ct)
        {
            const string sql = "SELECT " + SendTransactionColumns + @"
FROM   SentTransactions
WHERE  WalletId = @WalletId AND IdempotencyKey = @IdempotencyKey;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<SendTransactionEntity>(
                    new CommandDefinition(
                        sql,
                        new { WalletId = walletId, IdempotencyKey = idempotencyKey },
                        cancellationToken: ct))
                    .ConfigureAwait(false);
            }

        }

        public async Task InsertSentTransactionAsync(SendTransactionEntity entity, CancellationToken ct)
        {
            if (entity == null) 
            {
                throw new ArgumentNullException("Transaction");
            }

            const string sql = @"
INSERT INTO SentTransactions
    (TxId, WalletId, IdempotencyKey, ToAddress, AmountSats, FeeSats,
     VirtualSizeBytes, InputCount, ChangeAddress, ChangeSats,
     RawTransactionHex, BroadcastUtc)
VALUES
    (@TxId, @WalletId, @IdempotencyKey, @ToAddress, @AmountSats, @FeeSats,
     @VirtualSizeBytes, @InputCount, @ChangeAddress, @ChangeSats,
     @RawTransactionHex, @BroadcastUtc);";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
            }
        }
    }
}
