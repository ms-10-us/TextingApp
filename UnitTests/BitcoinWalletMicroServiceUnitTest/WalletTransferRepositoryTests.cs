using BitcoinWalletMicroService.Dapper;
using BitcoinWalletMicroService.DBSqlite;
using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroServiceUnitTest.TestSupport;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;

namespace BitcoinWalletMicroServiceUnitTest
{

    public sealed class WalletTransferRepositoryTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DbConnectionFactory _connectionFactory;
        private readonly WalletTransferRepository _sut;

        public WalletTransferRepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"transfer-test-{Guid.NewGuid():N}.db");

            _connectionFactory = new DbConnectionFactory(new WalletDbOptions
            {
                DbPath = _dbPath,
                BusyTimeoutMs = 5000
            });

            DapperConfig.Register();

            ApplySchema();
            SeedWallet();

            _sut = new WalletTransferRepository(_connectionFactory);
        }

        // -------------------------------------------------------------------
        // Sent transactions
        // -------------------------------------------------------------------

        [Fact]
        public async Task InsertSentTransaction_ThenGetByIdempotencyKey_RoundTrips()
        {
            SendTransactionEntity entity = TransferTestData.SendEntity();

            await _sut.InsertSentTransactionAsync(entity, CancellationToken.None);

            SendTransactionEntity? loaded = await _sut.GetSendTransactionByIdempotencyKeyAsync(
                TransferTestData.WalletId, TransferTestData.IdempotencyKey, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(entity.TxId, loaded.TxId);
            Assert.Equal(entity.AmountSats, loaded.AmountSats);
            Assert.Equal(entity.RawTransactionHex, loaded.RawTransactionHex);
        }

        [Fact]
        public async Task BroadcastUtc_RoundTripsAsUtc()
        {
            await _sut.InsertSentTransactionAsync(TransferTestData.SendEntity(), CancellationToken.None);

            SendTransactionEntity? loaded = await _sut.GetSendTransactionByIdempotencyKeyAsync(
                TransferTestData.WalletId, TransferTestData.IdempotencyKey, CancellationToken.None);

            Assert.Equal(DateTimeKind.Utc, loaded!.BroadcastUtc.Kind);
            Assert.Equal(TransferTestData.Now, loaded.BroadcastUtc);
        }

        [Fact]
        public async Task GetSendTransactionByIdempotencyKey_ReturnsNull_WhenUnknown()
        {
            Assert.Null(await _sut.GetSendTransactionByIdempotencyKeyAsync(
                TransferTestData.WalletId, "never-used", CancellationToken.None));
        }

        [Fact]
        public async Task InsertSentTransaction_RejectsDuplicateIdempotencyKey()
        {
            await _sut.InsertSentTransactionAsync(TransferTestData.SendEntity(), CancellationToken.None);

            SendTransactionEntity duplicate = TransferTestData.SendEntity();
            duplicate.TxId = "different-txid-but-same-key-0000000000000000000000000000000000";

            await Assert.ThrowsAsync<SqliteException>(
                () => _sut.InsertSentTransactionAsync(duplicate, CancellationToken.None));
        }

        [Fact]
        public async Task InsertSentTransaction_RejectsDuplicateTxId()
        {
            await _sut.InsertSentTransactionAsync(TransferTestData.SendEntity(), CancellationToken.None);

            SendTransactionEntity duplicate = TransferTestData.SendEntity();
            duplicate.IdempotencyKey = "a-different-key";

            await Assert.ThrowsAsync<SqliteException>(
                () => _sut.InsertSentTransactionAsync(duplicate, CancellationToken.None));
        }

        [Fact]
        public async Task InsertSentTransaction_ThrowsForNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _sut.InsertSentTransactionAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task GetSendTransactions_ReturnsNewestFirst()
        {
            SendTransactionEntity older = TransferTestData.SendEntity();
            older.TxId = "aaa1111111111111111111111111111111111111111111111111111111111111";
            older.IdempotencyKey = "key-older";
            older.BroadcastUtc = TransferTestData.Now.AddHours(-2);

            SendTransactionEntity newer = TransferTestData.SendEntity();
            newer.TxId = "bbb2222222222222222222222222222222222222222222222222222222222222";
            newer.IdempotencyKey = "key-newer";
            newer.BroadcastUtc = TransferTestData.Now;

            await _sut.InsertSentTransactionAsync(older, CancellationToken.None);
            await _sut.InsertSentTransactionAsync(newer, CancellationToken.None);

            List<SendTransactionEntity> rows =
                (await _sut.GetSendTransactionAsync(TransferTestData.WalletId, CancellationToken.None)).ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal(newer.TxId, rows[0].TxId);
        }

        [Fact]
        public async Task GetSendTransactions_IsScopedToTheWallet()
        {
            await _sut.InsertSentTransactionAsync(TransferTestData.SendEntity(), CancellationToken.None);

            Assert.Empty(await _sut.GetSendTransactionAsync("some-other-wallet", CancellationToken.None));
        }

        // -------------------------------------------------------------------
        // Deposits
        // -------------------------------------------------------------------

        [Fact]
        public async Task InsertDeposit_ThenGetById_RoundTrips()
        {
            DepositEntity deposit = TransferTestData.DepositEntity();

            await _sut.InsertDepositAsync(deposit, CancellationToken.None);

            DepositEntity? loaded = await _sut.GetDepositByIdAsync(
                TransferTestData.WalletId, TransferTestData.DepositId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(deposit.Address, loaded.Address);
            Assert.Equal(deposit.ExpectedSats, loaded.ExpectedSats);
            Assert.Equal("Pending", loaded.Status);
        }

        [Fact]
        public async Task GetDepositById_MapsBoolAndNullableColumns()
        {
            DepositEntity deposit = TransferTestData.DepositEntity(expectedSats: null);
            deposit.IsChange = true;

            await _sut.InsertDepositAsync(deposit, CancellationToken.None);

            DepositEntity? loaded = await _sut.GetDepositByIdAsync(
                TransferTestData.WalletId, TransferTestData.DepositId, CancellationToken.None);

            Assert.True(loaded!.IsChange);
            Assert.Null(loaded.ExpectedSats);
            Assert.Null(loaded.ConfirmedUtc);
            Assert.True(loaded.Id > 0);   
        }

        [Fact]
        public async Task GetDepositById_ReturnsNull_ForAnotherWalletsDeposit()
        {
            await _sut.InsertDepositAsync(TransferTestData.DepositEntity(), CancellationToken.None);

            Assert.Null(await _sut.GetDepositByIdAsync(
                "some-other-wallet", TransferTestData.DepositId, CancellationToken.None));
        }

        [Fact]
        public async Task InsertDeposit_RejectsDuplicateAddress()
        {
            await _sut.InsertDepositAsync(TransferTestData.DepositEntity(), CancellationToken.None);

            DepositEntity duplicate = TransferTestData.DepositEntity();
            duplicate.DepositId = Guid.NewGuid().ToString("D");

            // Two deposits on one address would let a single payment satisfy both.
            await Assert.ThrowsAsync<SqliteException>(
                () => _sut.InsertDepositAsync(duplicate, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateDeposit_PersistsStatusAndAmount()
        {
            DepositEntity deposit = TransferTestData.DepositEntity();
            await _sut.InsertDepositAsync(deposit, CancellationToken.None);

            deposit.Status = "Confirmed";
            deposit.ReceivedSats = 50_000;
            deposit.TxId = "cc33333333333333333333333333333333333333333333333333333333333333";
            deposit.ConfirmedUtc = TransferTestData.Now.AddMinutes(30);

            await _sut.UpdateDepositAsync(deposit, CancellationToken.None);

            DepositEntity? loaded = await _sut.GetDepositByIdAsync(
                TransferTestData.WalletId, TransferTestData.DepositId, CancellationToken.None);

            Assert.Equal("Confirmed", loaded!.Status);
            Assert.Equal(50_000, loaded.ReceivedSats);
            Assert.Equal(deposit.TxId, loaded.TxId);
            Assert.Equal(DateTimeKind.Utc, loaded.ConfirmedUtc!.Value.Kind);
        }

        [Fact]
        public async Task UpdateDeposit_DoesNotChangeTheAddress()
        {
            DepositEntity deposit = TransferTestData.DepositEntity();
            await _sut.InsertDepositAsync(deposit, CancellationToken.None);

            string originalAddress = deposit.Address;
            deposit.Address = "tb1qcompletelydifferentaddress0000000000000";
            deposit.Status = "Detected";

            await _sut.UpdateDepositAsync(deposit, CancellationToken.None);

            DepositEntity? loaded = await _sut.GetDepositByIdAsync(
                TransferTestData.WalletId, TransferTestData.DepositId, CancellationToken.None);

            Assert.Equal(originalAddress, loaded!.Address);
            Assert.Equal("Detected", loaded.Status);
        }

        [Fact]
        public async Task GetDeposits_ReturnsNewestFirst()
        {
            DepositEntity older = TransferTestData.DepositEntity();
            older.DepositId = Guid.NewGuid().ToString("D");
            older.Address = "tb1qolderaddress000000000000000000000000000";
            older.CreatedUtc = TransferTestData.Now.AddHours(-3);

            DepositEntity newer = TransferTestData.DepositEntity();
            newer.DepositId = Guid.NewGuid().ToString("D");
            newer.Address = "tb1qneweraddress00000000000000000000000000";
            newer.CreatedUtc = TransferTestData.Now;

            await _sut.InsertDepositAsync(older, CancellationToken.None);
            await _sut.InsertDepositAsync(newer, CancellationToken.None);

            List<DepositEntity> rows =
                (await _sut.GetDepositsAsync(TransferTestData.WalletId, CancellationToken.None)).ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal(newer.DepositId, rows[0].DepositId);
        }

        [Fact]
        public async Task GetDeposits_ReturnsEmpty_ForUnknownWallet()
        {
            await _sut.InsertDepositAsync(TransferTestData.DepositEntity(), CancellationToken.None);

            Assert.Empty(await _sut.GetDepositsAsync("some-other-wallet", CancellationToken.None));
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void ApplySchema()
        {
            const string schema = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Wallets (
    Id                       TEXT     NOT NULL PRIMARY KEY,
    Label                    TEXT     NOT NULL,
    Network                  TEXT     NOT NULL,
    EncryptedMnemonic        BLOB     NOT NULL,
    MnemonicFingerprint      TEXT     NOT NULL,
    AccountExtendedPublicKey TEXT     NOT NULL,
    AccountDerivationPath    TEXT     NOT NULL,
    CreatedUtc               TEXT     NOT NULL
);

CREATE TABLE IF NOT EXISTS SentTransactions (
    Id                INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
    TxId              TEXT     NOT NULL,
    WalletId          TEXT     NOT NULL,
    IdempotencyKey    TEXT     NOT NULL,
    ToAddress         TEXT     NOT NULL,
    AmountSats        INTEGER  NOT NULL,
    FeeSats           INTEGER  NOT NULL,
    VirtualSizeBytes  INTEGER  NOT NULL,
    InputCount        INTEGER  NOT NULL,
    ChangeAddress     TEXT     NULL,
    ChangeSats        INTEGER  NOT NULL,
    RawTransactionHex TEXT     NOT NULL,
    BroadcastUtc      TEXT     NOT NULL,
    CONSTRAINT FK_SentTransactions_Wallets
        FOREIGN KEY (WalletId) REFERENCES Wallets (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_SentTransactions_Idempotency
    ON SentTransactions (WalletId, IdempotencyKey);

CREATE UNIQUE INDEX IF NOT EXISTS UX_SentTransactions_TxId
    ON SentTransactions (TxId);

CREATE TABLE IF NOT EXISTS Deposits (
    Id            INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
    DepositId     TEXT     NOT NULL,
    WalletId      TEXT     NOT NULL,
    Address       TEXT     NOT NULL,
    AddressIndex  INTEGER  NOT NULL,
    IsChange      INTEGER  NOT NULL,
    ExpectedSats  INTEGER  NULL,
    Label         TEXT     NULL,
    ReceivedSats  INTEGER  NOT NULL DEFAULT 0,
    Status        TEXT     NOT NULL,
    TxId          TEXT     NULL,
    CreatedUtc    TEXT     NOT NULL,
    ExpiresUtc    TEXT     NOT NULL,
    ConfirmedUtc  TEXT     NULL,
    CONSTRAINT FK_Deposits_Wallets
        FOREIGN KEY (WalletId) REFERENCES Wallets (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Deposits_DepositId
    ON Deposits (DepositId);

CREATE UNIQUE INDEX IF NOT EXISTS UX_Deposits_Address
    ON Deposits (Address);";

            using IDbConnection connection = _connectionFactory.CreateOpenConnection();
            using IDbCommand command = connection.CreateCommand();
            command.CommandText = schema;
            command.ExecuteNonQuery();
        }

        private void SeedWallet()
        {
            using IDbConnection connection = _connectionFactory.CreateOpenConnection();
            using IDbCommand command = connection.CreateCommand();

            command.CommandText =
                "INSERT INTO Wallets (Id, Label, Network, EncryptedMnemonic, MnemonicFingerprint, " +
                "AccountExtendedPublicKey, AccountDerivationPath, CreatedUtc) " +
                $"VALUES ('{TransferTestData.WalletId}', 'Test', 'TestNet', X'01', 'fp', " +
                "'vpub', 'm/84''/1''/0''', '2026-03-01T12:00:00.0000000Z');";

            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            foreach (string path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch (IOException) { /* temp file */ }
                }
            }
        }
    }
}