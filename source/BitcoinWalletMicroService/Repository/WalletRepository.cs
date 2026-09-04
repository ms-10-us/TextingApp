using BitcoinWalletMicroService.DBSqlite;
using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Models;
using Dapper;
using System.Data;
using System.Globalization;

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
WHERE  MnemonicFingerprint = @Fingerprint;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                var row = await connection.QuerySingleOrDefaultAsync<WalletEntity>(
                    new CommandDefinition(sql, new { Fingerprint = fingerprint}, cancellationToken: ct))
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

        public async Task<IEnumerable<WalletSummaryEntity>> GetWalletSummariesAsync(CancellationToken ct = default(CancellationToken))
        {
            const string sql = @"
SELECT w.Id                       AS WalletId,
       w.Label                    AS Label,
       w.Network                  AS Network,
       w.AccountExtendedPublicKey AS AccountExtendedPublicKey,
       w.AccountDerivationPath    AS AccountDerivationPath,
       w.CreatedUtc               AS CreatedUtc,
       COUNT(k.Id)                AS AddressCount
FROM   Wallets w
LEFT JOIN DerivedKeys k ON k.WalletId = w.Id
GROUP BY w.Id, w.Label, w.Network, w.AccountExtendedPublicKey,
         w.AccountDerivationPath, w.CreatedUtc
ORDER BY w.CreatedUtc DESC;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                var rows = await connection.QueryAsync<dynamic>(
                    new CommandDefinition(sql, cancellationToken: ct))
                    .ConfigureAwait(false);

                return rows.Select(r => new WalletSummaryEntity
                {
                    WalletId = r.WalletId,
                    Label = r.Label,
                    Network = r.Network,
                    AccountExtendedPublicKey = r.AccountExtendedPublicKey,
                    AccountDerivationPath = r.AccountDerivationPath,
                    AddressCount = (int)r.AddressCount,
                    CreatedUtc = ToUtc(r.CreatedUtc)
                }).ToList();
            }
        }


        public async Task<WalletEntity> GetWalletByIdAsync(string walletId, CancellationToken ct = default(CancellationToken))
        {
            const string sql = @"
SELECT Id, Label, Network, EncryptedMnemonic, MnemonicFingerprint,
       AccountExtendedPublicKey, AccountDerivationPath, CreatedUtc
FROM Wallets
WHERE Id = @WalletId;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
                    new CommandDefinition(sql, new { WalletId = walletId }, cancellationToken: ct))
                    .ConfigureAwait(false);

                if (row == null)
                {
                    return null;
                }

                WalletEntity entity = new WalletEntity
                {
                    Id = walletId,
                    Label = row.Label,
                    Network = row.Network,
                    EncryptedMnemonic = row.EncryptedMnemonic,
                    MnemonicFingerprint = row.MnemonicFingerprint,
                    AccountExtendedPublicKey = row.AccountExtendedPublicKey,
                    AccountDerivationPath = row.AccountDerivationPath,
                    CreatedUtc = ToUtc(row.CreatedUtc)
                };

                return entity;
            }
        }

        public async Task<IEnumerable<DerivedKeyEntity>> GetDerivedKeysAsync(
            string walletId,
            bool? isChange,
            CancellationToken ct = default(CancellationToken))
        {
            const string sql = @"
SELECT Id, WalletId, IsChange, AddressIndex, DerivationPath,
       PublicKeyHex, Address, CreatedUtc
FROM DerivedKeys
WHERE WalletId = @WalletId
AND   (@IsChange IS NULL OR IsChange = @IsChange)
ORDER BY IsChange, AddressIndex;";

            var paramerters = new
            {
                WalletId = walletId,
                IsChange = isChange.HasValue ? (int?)(isChange.Value ? 1 : 0) : null
            };

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                var rows = await connection.QueryAsync<dynamic>(
                    new CommandDefinition(sql, paramerters, cancellationToken: ct))
                    .ConfigureAwait (false);

                return rows.Select(r => new DerivedKeyEntity
                {
                    Id = (long)r.Id,
                    WalletId = (string)r.WalletId,
                    IsChange = (long)r.IsChange != 0,
                    AddressIndex = (int)(long)r.AddressIndex,
                    DerivationPath = (string)r.DerivationPath,
                    PublicKeyHex = (string)r.PublicKeyHex,
                    Address = (string)r.Address,
                    CreatedUtc = ToUtc((string)r.CreatedUtc)
                }).ToList();
            }
        }

        public async Task<bool> DeleteWalletAsync(string walletId, CancellationToken ct = default(CancellationToken))
        {
            const string sql = "DELETE FROM Wallets WHERE Id = @WalletId;";

            using (IDbConnection connection = _connectionFactory.CreateOpenConnection())
            {
                int rowsAffected = await connection.ExecuteAsync(
                    new CommandDefinition(sql, new { WalletId = walletId }, cancellationToken: ct))
                    .ConfigureAwait(false);

                return (rowsAffected > 0);
            }
        }

        private static string ToIso(DateTime value)
        {
            return value.ToUniversalTime().ToString("o");
        }

        private static DateTime ToUtc(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }
}
