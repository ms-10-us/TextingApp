using BitcoinWalletMicroService.Dapper;
using BitcoinWalletMicroService.DBSqlite;
using BitcoinWalletMicroService.Entities;
using BitcoinWalletMicroService.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;

namespace BitcoinWalletMicroServiceUnitTest
{
    public sealed class WalletRepositoryTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DbConnectionFactory _connectionFactory;
        private readonly WalletRepository _sut;    

        public WalletRepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"wallet-test-{Guid.NewGuid():N}.db");

            _connectionFactory = new DbConnectionFactory(new WalletDbOptions
            {
                DbPath = _dbPath,
                BusyTimeoutMs = 5000
            });

            DapperConfig.Register();

            ApplySchema();

            _sut = new WalletRepository(_connectionFactory);
        }


        // ------------------------------------------------------------------- InsertWalletAsync

        [Fact]
        public async Task GivenWallet_WhenCallingInsertWalletAsync_ThenRoundTrips()
        {
            WalletEntity wallet = TestData.Wallet();

            await _sut.InsertWalletAsync(wallet);
            WalletEntity? loaded = await _sut.GetWalletByIdAsync(wallet.Id);

            Assert.NotNull(loaded);
            Assert.Equal(wallet.Id, loaded.Id);
            Assert.Equal(wallet.Label, loaded.Label);
            Assert.Equal(wallet.AccountExtendedPublicKey, loaded.AccountExtendedPublicKey);
            Assert.Equal(wallet.EncryptedMnemonic, loaded.EncryptedMnemonic);
        }

        [Fact]
        public async Task GivenWalletCreated_WhenCallingInsertWalletAsync_RoundTripsAsUtc()
        {
            WalletEntity wallet = TestData.Wallet();

            await _sut.InsertWalletAsync(wallet);
            WalletEntity? loaded = await _sut.GetWalletByIdAsync(wallet.Id);

            Assert.Equal(DateTimeKind.Utc, loaded!.CreatedUtc.Kind);
            Assert.Equal(wallet.CreatedUtc, loaded.CreatedUtc);
        }

        // ------------------------------------------------------------------- GetWalletById

        [Fact]
        public async Task GivenWalletMissing_WhenCallingGetWalletById_ReturnsNull()
        {
            Assert.Null(await _sut.GetWalletByIdAsync("does-not-exist"));
        }

        // ------------------------------------------------------------------- GetWalletByFingerprint

        [Fact]
        public async Task GivenWallet_WhenCallingGetWalletByFingerprint_FindsWallet()
        {
            WalletEntity wallet = TestData.Wallet();
            await _sut.InsertWalletAsync(wallet);

            WalletEntity? loaded = await _sut.GetWalletByFingerprintAsync(wallet.MnemonicFingerprint);

            Assert.NotNull(loaded);
            Assert.Equal(wallet.Id, loaded.Id);
        }

        // ------------------------------------------------------------------- InsertDerivedKeyAsync

        [Fact]
        public async Task GivenDerivedKeys_WhenCallingDerivedKeysAsync_FindsKeysByWalletId()
        {
            await SeedWalletAsync();

            await _sut.InsertDerivedKeyAsync([
                TestData.KeyEntity(0),
                TestData.KeyEntity(1),
                TestData.KeyEntity(2)
                ]);

            IEnumerable<DerivedKeyEntity> keys = await _sut.GetDerivedKeysAsync(TestData.WalletId, null);

            Assert.Equal(3, keys.Count());
        }

        [Fact]
        public async Task GivenDerivedKeys_WhenCallingDerivedKeysAsync_ThenFiltersByChain()
        {
            await SeedWalletAsync();

            await _sut.InsertDerivedKeyAsync([
                TestData.KeyEntity(0, isChange: false),
                TestData.KeyEntity(1, isChange: false),
                TestData.KeyEntity(0, isChange: true)
                ]);

            Assert.Equal(2, (await _sut.GetDerivedKeysAsync(TestData.WalletId, false)).Count());
            Assert.Single(await _sut.GetDerivedKeysAsync(TestData.WalletId, true));
            Assert.Equal(3, (await _sut.GetDerivedKeysAsync(TestData.WalletId, null)).Count());
        }

        // ------------------------------------------------------------------- GetDerivedKeyAsync

        [Fact]
        public async Task GivenDerivedKeys_WhenCallingGetDerivedKeysAsync_MapsBoolAndINtColumnsCorrectly()
        {
            await SeedWalletAsync();
            await _sut.InsertDerivedKeyAsync([TestData.KeyEntity(7, isChange: true)]);

            DerivedKeyEntity key = (await _sut.GetDerivedKeysAsync(TestData.WalletId, true)).Single();

            Assert.True(key.IsChange);
            Assert.Equal(7, key.AddressIndex);
            Assert.True(key.Id > 0);
            Assert.Null(key.PrivateKeyWif);
        }

        [Fact]
        public async Task GivenUnkownWallet_WhenCallingGetDerivedKeysAsync_ReturnsEmpty()
        {
            await SeedWalletAsync();
            await _sut.InsertDerivedKeyAsync([TestData.KeyEntity(0)]);

            Assert.Empty(await _sut.GetDerivedKeysAsync("some-other-wallet", null));
        }

        [Fact]
        public async Task GivenEmptyCollection_WhenCallingGetDerivedKeysAsync_ThenNoOp()
        {
            await SeedWalletAsync();

            await _sut.InsertDerivedKeyAsync([]);

            Assert.Empty(await _sut.GetDerivedKeysAsync(TestData.WalletId, null));
        }

        // ------------------------------------------------------------------- GetWalletSummariesAsync

        [Fact]
        public async Task GivenWalletSummary_WhenCallingGetWalletSummariesAsync_ThenCountsAddresses()
        {
            await SeedWalletAsync();
            await _sut.InsertDerivedKeyAsync([TestData.KeyEntity(0), TestData.KeyEntity(1)]);

            WalletSummaryEntity summary = (await _sut.GetWalletSummariesAsync()).Single();

            Assert.Equal(TestData.WalletId, summary.WalletId);
            Assert.Equal(2, summary.AddressCount);
        }

        [Fact]
        public async Task GivenNoAddresses_WhenCallingGetWalletSummarries_ThenReturnsZeroCount()
        {
            await SeedWalletAsync();

            WalletSummaryEntity summary = (await _sut.GetWalletSummariesAsync()).Single();

            Assert.Equal(0, summary.AddressCount);
        }

        // ------------------------------------------------------------------- DeleteWallet

        [Fact]
        public async Task GivenWallet_WhenCallingDeleteWallet_CasacadesToDerivedKeys()
        {
            await SeedWalletAsync();
            await _sut.InsertDerivedKeyAsync([TestData.KeyEntity(0), TestData.KeyEntity(1)]);

            bool delete = await _sut.DeleteWalletAsync(TestData.WalletId);

            Assert.True(delete);
            Assert.Null(await _sut.GetWalletByIdAsync(TestData.WalletId));
            Assert.Empty(await _sut.GetDerivedKeysAsync(TestData.WalletId, null));
        }

        [Fact]
        public async Task GivenNoWallet_WhenCallingDeleteWallet_ThenReturnsNothing()
        {
            Assert.False(await _sut.DeleteWalletAsync("does-not-exist"));
        }

        private void ApplySchema()
        {
            // Inline rather than reading SQLScripts/Schema.sql: the test then does not
            // depend on that file's build action or on the output directory layout.
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

CREATE UNIQUE INDEX IF NOT EXISTS UX_Wallets_Fingerprint
    ON Wallets (MnemonicFingerprint);

CREATE TABLE IF NOT EXISTS DerivedKeys (
    Id             INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
    WalletId       TEXT     NOT NULL,
    IsChange       INTEGER  NOT NULL,
    AddressIndex   INTEGER  NOT NULL,
    DerivationPath TEXT     NOT NULL,
    PublicKeyHex   TEXT     NOT NULL,
    Address        TEXT     NOT NULL,
    CreatedUtc     TEXT     NOT NULL,
    CONSTRAINT FK_DerivedKeys_Wallets
        FOREIGN KEY (WalletId) REFERENCES Wallets (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_DerivedKeys_Path
    ON DerivedKeys (WalletId, IsChange, AddressIndex);

CREATE INDEX IF NOT EXISTS IX_DerivedKeys_Address
    ON DerivedKeys (Address);";

            using IDbConnection connection = _connectionFactory.CreateOpenConnection();
            using IDbCommand command = connection.CreateCommand();
            command.CommandText = schema;
            command.ExecuteNonQuery();
        }


        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            foreach (string path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch (IOException) { /* temp file, ignore */ }
                }
            }
        }

        private async Task SeedWalletAsync() => await _sut.InsertWalletAsync(TestData.Wallet());
    }
}
