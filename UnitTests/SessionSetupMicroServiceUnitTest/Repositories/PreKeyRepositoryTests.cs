using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.PostgresDB;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Repositories
{
    public class PreKeyRepositoryTests
    {
        private readonly FakeDb _db = new();
        private readonly PreKeyRepository _preKeys;
        private readonly ProtocolAddress _address = Any.Address(deviceId: 1);

        public PreKeyRepositoryTests()
        {
            _preKeys = new PreKeyRepository(new PostgresConnectionScope(_db.DataSource));
        }

        [Fact]
        public async Task UpsertSignedPreKeyAsync_BindsTheKindAsText()
        {
            await _preKeys.UpsertSignedPreKeyAsync(_address, Any.SignedCurvePreKey(keyId: 4));

            var command = _db.Single();
            Assert.Equal(SessionSetupSql.UpsertSignedPreKey, command.Sql);

            // Enum parameters travel as text and are cast in SQL, so the data layer needs no
            // provider-specific enum mapping. "Curve" or 1 would both be wrong.
            Assert.Equal("curve", command.Parameter("@kind"));
            Assert.Equal(4L, command.Parameter("@key_id"));
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_WithNoKeys_SendsNothing()
        {
            var added = await _preKeys.AddOneTimePreKeysAsync(_address, Array.Empty<OneTimePreKey>());

            Assert.Equal(0, added);
            Assert.Empty(_db.Executed);
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_SendsOneStatementPerKind()
        {
            await _preKeys.AddOneTimePreKeysAsync(_address, new[]
            {
                Any.OneTimeCurve(1), Any.OneTimeCurve(2), Any.OneTimeKyber(1)
            });

            var commands = _db.For(SessionSetupSql.AddOneTimePreKeys).ToList();

            // Bulk insert per kind, not per key: a 200-key upload is two round trips.
            Assert.Equal(2, commands.Count);
            Assert.Contains(commands, command => (string?)command.Parameter("@kind") == "curve");
            Assert.Contains(commands, command => (string?)command.Parameter("@kind") == "kyber");
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_SendsCurveSignaturesAsEmptyArrays()
        {
            await _preKeys.AddOneTimePreKeysAsync(_address, new[] { Any.OneTimeCurve(1), Any.OneTimeCurve(2) });

            var signatures = Assert.IsType<byte[][]>(_db.Single().Parameter("@signatures"));

            // Curve one-time keys carry no signature of their own. They are bound as empty arrays
            // and the SQL turns them into NULL with nullif(k.signature, ''::bytea) — which is what
            // the Kyber CHECK constraint in the schema depends on.
            Assert.All(signatures, signature => Assert.Empty(signature));
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_SendsKyberSignaturesIntact()
        {
            await _preKeys.AddOneTimePreKeysAsync(_address, new[] { Any.OneTimeKyber(1) });

            var signatures = Assert.IsType<byte[][]>(_db.Single().Parameter("@signatures"));

            Assert.All(signatures, signature => Assert.Equal(Any.SignatureBytes, signature.Length));
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_BindsIdsAndKeysInMatchingOrder()
        {
            await _preKeys.AddOneTimePreKeysAsync(_address, new[] { Any.OneTimeCurve(7), Any.OneTimeCurve(9) });

            var command = _db.Single();
            var ids = Assert.IsType<long[]>(command.Parameter("@key_ids"));
            var keys = Assert.IsType<byte[][]>(command.Parameter("@public_keys"));

            // The statement unnests three arrays side by side. If they ever fall out of step, a key
            // is stored under another key's id — and nothing would notice until a handshake failed.
            Assert.Equal(new[] { 7L, 9L }, ids);
            Assert.Equal(2, keys.Length);
        }

        [Fact]
        public async Task GetSignedPreKeyAsync_WithNoRow_ReturnsNull()
        {
            Assert.Null(await _preKeys.GetSignedPreKeyAsync(_address, PreKeyKind.Curve));
            Assert.Equal(SessionSetupSql.GetSignedPreKey, _db.Single().Sql);
        }

        [Fact]
        public async Task GetSignedPreKeyAsync_MapsTheRowAndDerivesTheAlgorithmFromTheKind()
        {
            var publicKey = Any.Bytes(Any.KyberKeyBytes);
            _db.RowsFor = _ => new[]
            {
                new FakeRow()
                    .Set("account_id", _address.Account.Value)
                    .Set("device_id", 1)
                    .Set("kind", "kyber")
                    .Set("key_id", 12L)
                    .Set("public_key", publicKey)
                    .Set("signature", Any.Bytes(Any.SignatureBytes))
                    .Set("created_at", FixedClock.Default)
            };

            var key = await _preKeys.GetSignedPreKeyAsync(_address, PreKeyKind.Kyber);

            Assert.NotNull(key);
            Assert.Equal(PreKeyKind.Kyber, key!.Kind);
            Assert.Equal(12, key.Id.Value);

            // The algorithm is not a column: it follows from the kind, so the two can never disagree.
            Assert.Equal(KeyAlgorithms.Kyber1024, key.PublicKey.Algorithm);
            Assert.Equal(publicKey, key.PublicKey.Value);
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_SendsTheAtomicPopStatement()
        {
            await _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve);

            var command = _db.Single();
            Assert.Equal(SessionSetupSql.TakeOneTimePreKey, command.Sql);

            // These three clauses are the entire guarantee that a one-time prekey is issued at most
            // once. Rewriting this statement without them compiles, runs, passes review, and
            // silently weakens the forward secrecy of two conversations — so assert on the text.
            Assert.Contains("for update skip locked", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("delete from", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("returning", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("curve", command.Parameter("@kind"));
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_OnAnEmptyPool_ReturnsNull()
        {
            // Null means "no row". Turning that into "degrade the protocol" is the orchestrator's
            // job, not the repository's.
            Assert.Null(await _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve));
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_MapsACurveRowWithANullSignature()
        {
            _db.RowsFor = _ => new[]
            {
                new FakeRow()
                    .Set("account_id", _address.Account.Value)
                    .Set("device_id", 1)
                    .Set("kind", "curve")
                    .Set("key_id", 5L)
                    .Set("public_key", Any.Bytes(Any.CurveKeyBytes))
                    .Set("signature", null)
                    .Set("created_at", FixedClock.Default)
            };

            var key = await _preKeys.TakeOneTimePreKeyAsync(_address, PreKeyKind.Curve);

            Assert.NotNull(key);
            Assert.Equal(5, key!.Id.Value);
            Assert.Null(key.Signature);
            Assert.False(key.IsSigned);
        }

        [Fact]
        public async Task CountAsync_MapsTheGroupedCounts()
        {
            _db.RowsFor = _ => new[]
            {
                new FakeRow().Set("kind", "curve").Set("count", 17L),
                new FakeRow().Set("kind", "kyber").Set("count", 4L)
            };

            var inventory = await _preKeys.CountAsync(_address);

            Assert.Equal(17, inventory.Curve);
            Assert.Equal(4, inventory.Kyber);
        }

        [Fact]
        public async Task CountAsync_WithNoRows_IsZeroForBothKinds()
        {
            var inventory = await _preKeys.CountAsync(_address);

            // `group by kind` returns no row for an empty pool rather than a zero, so the mapping
            // has to start at zero and stay there.
            Assert.Equal(0, inventory.Curve);
            Assert.Equal(0, inventory.Kyber);
        }

        [Fact]
        public async Task CountAsync_WithOnlyOneKindPresent_LeavesTheOtherAtZero()
        {
            _db.RowsFor = _ => new[] { new FakeRow().Set("kind", "kyber").Set("count", 9L) };

            var inventory = await _preKeys.CountAsync(_address);

            Assert.Equal(0, inventory.Curve);
            Assert.Equal(9, inventory.Kyber);
        }
    }
}
