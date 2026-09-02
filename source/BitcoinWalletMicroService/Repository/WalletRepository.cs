using BitcoinWalletMicroService.DBSqlite;
using BitcoinWalletMicroService.Entities;
using Dapper;
using System.Data;

namespace BitcoinWalletMicroService.Repository
{
    public class WalletRepository : IWalletRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public WalletRepository(IDbConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            _connectionFactory = connectionFactory;
        }


        public async Task<WalletEntity?> GetWalletByFingerprintAsync(string fingerprint, CancellationToken ct = default)
        {
            const string sql = @"
SELECT Id, Label, Network, EncryptedMnemonic, MnemonicFingerprint, AccountExtendedPublicKey, AccountDerivationPath, CreatedUtc
FROM   Wallets
WHERE  MemonicFingerprint = @Fingerprint;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                var row = await connection.QuerySingleOrDefaultAsync<WalletEntity>(
                    new CommandDefinition(sql, new { FingerPrint = fingerprint}, cancellationToken: ct))
                    .ConfigureAwait(false);

                return row;
            }
        }

        public async Task InsertDerivedKeyAsync(
            IEnumerable<DerivedKeyEntity> keys, 
            IDbTransaction transaction = null, 
            CancellationToken ct = default)
        {
            if (keys == null)
            {
                throw new ArgumentNullException("keys");
            }

            var payload = keys.Select(k => new
            {
                k.WalletId,
                k.IsChange,
                k.AddressIndex,
                k.DerivationPath,
                k.PublicKeyHex,
                k.Address,
                CreatedUtc = ToIso(k.CreatedUtc)
            });

            if (payload.Count() == 0)
            {
                return;
            }

            const string sql = @"
INSERT INTO DerivedKeys
    (WalletId, IsChange, AddressIndex, DerivationPath, PublicKeyHex, Address, CreatedUtc)
VALUES
    (@WalletId, @IsChange, @AddressIndex, @DerivationPath, @PublicKeyHex, @Address, @CreatedUtc);";

            if (transaction != null)
            {
                await transaction.Connection
                    .ExecuteAsync(new CommandDefinition(sql, payload, transaction, cancellationToken: ct))
                    .ConfigureAwait(false);

                return;
            }

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                using (IDbTransaction localTransaction = connection.BeginTransaction())
                {
                    await connection
                        .ExecuteAsync(new CommandDefinition(sql, payload, localTransaction, cancellationToken: ct))
                        .ConfigureAwait(false);

                    localTransaction.Commit();
                }
            }
        }

        public async Task<string> InsertWalletAsync(
            WalletEntity wallet, 
            IDbTransaction transaction = null, 
            CancellationToken ct = default)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException("wallet");
            }

            const string sql = @"
INSERT INTO WALLETS
    (Id, Label, Network, EncryptedMnemonic, MnemonicFingerprint,
     AccountExtendedPublicKey, AccountDerivationPath, CreatedUtc)
VALUES
    (@Id, @Label, @Network, @EncryptedMnemonic, @MnemonicFingerprint,
     @AccountExtendedPublicKey, @AccountDerivationPath, @CreatedUtc);";

            var parameters = new
            {
                wallet.Id,
                wallet.Label,
                wallet.Network,
                wallet.EncryptedMnemonic,
                wallet.MnemonicFingerprint,
                wallet.AccountExtendedPublicKey,
                wallet.AccountDerivationPath,
                CreatedUtc = ToIso(wallet.CreatedUtc)
            };

            if (transaction != null)
            {
                await transaction.Connection
                    .ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct))
                    .ConfigureAwait(false);

                return wallet.Id;
            }

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                await connection
                    .ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct))
                    .ConfigureAwait(false);

                return wallet.Id;
            }
        }

        private static string ToIso(DateTime value)
        {
            return value.ToUniversalTime().ToString("o");
        }

    }
}
