using SessionSetupMicroService.Models;
using SessionSetupMicroService.PostgresDB;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Repositories
{
    /// <summary>
    /// Drives DeviceRepository through a fake DbDataSource.
    ///
    /// What these tests check is the wiring: which statement was sent, which parameters were bound
    /// to it, and how a returned row is mapped. They do NOT check that the SQL is correct
    /// PostgreSQL — for that you need a real database, and no in-process fake can substitute.
    /// </summary>
    public class DeviceRepositoryTests
    {
        private readonly FakeDb _db = new();
        private readonly DeviceRepository _devices;
        private readonly ProtocolAddress _address = Any.Address(deviceId: 2);

        public DeviceRepositoryTests()
        {
            _devices = new DeviceRepository(new PostgresConnectionScope(_db.DataSource));
        }

        [Fact]
        public async Task EnsureAccountAsync_SendsTheIdempotentInsert()
        {
            await _devices.EnsureAccountAsync(_address.Account);

            var command = _db.Single();
            Assert.Equal(SessionSetupSql.EnsureAccount, command.Sql);
            Assert.Equal(_address.Account.Value, command.Parameter("@account_id"));
        }

        [Fact]
        public async Task InsertAsync_BindsEverySevenColumn()
        {
            var registration = Any.Registration(displayName: "Mazen laptop", registrationId: 1234);
            var hash = Any.Bytes(32, seed: 9);

            await _devices.InsertAsync(_address, registration, hash);

            var command = _db.Single();
            Assert.Equal(SessionSetupSql.InsertDevice, command.Sql);
            Assert.Equal(_address.Account.Value, command.Parameter("@account_id"));
            Assert.Equal(2, command.Parameter("@device_id"));
            Assert.Equal("Mazen laptop", command.Parameter("@display_name"));
            Assert.Equal(1234, command.Parameter("@registration_id"));
            Assert.Equal(KeyAlgorithms.Ed25519, command.Parameter("@identity_algorithm"));
            Assert.Equal(registration.IdentityKey.Value, command.Parameter("@identity_key"));

            // The credential itself is never bound — only its hash reaches the database.
            Assert.Equal(hash, command.Parameter("@credential_hash"));
        }

        [Fact]
        public async Task InsertAsync_ReportsWhetherARowWasWritten()
        {
            _db.NonQueryFor = _ => 1;
            Assert.True(await _devices.InsertAsync(_address, Any.Registration(), Any.Bytes(32)));

            // `on conflict do nothing` reports a duplicate as zero rows rather than as an Npgsql
            // 23505. That is what keeps the driver's error codes out of the orchestrator.
            _db.NonQueryFor = _ => 0;
            Assert.False(await _devices.InsertAsync(_address, Any.Registration(), Any.Bytes(32)));
        }

        [Fact]
        public async Task LockAccountAsync_SendsTheSelectForUpdate()
        {
            _db.ScalarFor = _ => 1;

            Assert.True(await _devices.LockAccountAsync(_address.Account));

            var command = _db.Single();
            Assert.Equal(SessionSetupSql.LockAccount, command.Sql);
            Assert.Equal(_address.Account.Value, command.Parameter("@account_id"));

            // FOR UPDATE is the entire point of this statement. Without it two concurrent links
            // compute the same device id and the second insert dies on the primary key.
            Assert.Contains("for update", command.Sql, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LockAccountAsync_ForAnAccountThatDoesNotExist_IsFalse()
        {
            _db.ScalarFor = _ => null;

            Assert.False(await _devices.LockAccountAsync(_address.Account));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        public async Task NextDeviceIdAsync_MapsTheScalar(int next)
        {
            _db.ScalarFor = _ => next;

            var deviceId = await _devices.NextDeviceIdAsync(_address.Account);

            Assert.Equal(next, deviceId.Value);
            Assert.Equal(SessionSetupSql.NextDeviceId, _db.Single().Sql);
        }

        [Fact]
        public async Task NextDeviceIdAsync_AcceptsAWidenedScalar()
        {
            // coalesce(max(...), 0) + 1 can come back as a wider integer type depending on the
            // column and the driver, so the mapping converts rather than casts.
            _db.ScalarFor = _ => 3L;

            Assert.Equal(3, (await _devices.NextDeviceIdAsync(_address.Account)).Value);
        }

        [Fact]
        public async Task FindAsync_WithNoRow_ReturnsNull()
        {
            Assert.Null(await _devices.FindAsync(_address));
            Assert.Equal(SessionSetupSql.FindDevice, _db.Single().Sql);
        }

        [Fact]
        public async Task FindAsync_MapsTheRowOntoTheDomainModel()
        {
            var registeredAt = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
            var lastSeen = registeredAt.AddDays(2);
            var identity = Any.Bytes(32, seed: 4);

            _db.RowsFor = _ => new[]
            {
                new FakeRow()
                    .Set("account_id", _address.Account.Value)
                    .Set("device_id", 2)
                    .Set("display_name", "Mazen laptop")
                    .Set("registration_id", 1234)
                    .Set("identity_algorithm", KeyAlgorithms.Ed25519)
                    .Set("identity_key", identity)
                    .Set("credential_hash", Any.Bytes(32))
                    .Set("registered_at", registeredAt)
                    .Set("last_seen_at", lastSeen)
            };

            var device = await _devices.FindAsync(_address);

            Assert.NotNull(device);
            Assert.Equal(_address, device!.Address);
            Assert.Equal("Mazen laptop", device.DisplayName);
            Assert.Equal(1234, device.RegistrationId.Value);
            Assert.Equal(KeyAlgorithms.Ed25519, device.IdentityKey.Algorithm);
            Assert.Equal(identity, device.IdentityKey.Value);
            Assert.Equal(registeredAt, device.RegisteredAt);
            Assert.Equal(lastSeen, device.LastSeen);
        }

        [Fact]
        public async Task ListByAccountAsync_MapsEveryRow()
        {
            _db.RowsFor = _ => new[] { Row(1), Row(2), Row(3) };

            var devices = (await _devices.ListByAccountAsync(_address.Account)).ToList();

            Assert.Equal(3, devices.Count);
            Assert.Equal(new[] { 1, 2, 3 }, devices.Select(device => device.Address.Device.Value));
            Assert.Equal(SessionSetupSql.ListDevicesByAccount, _db.Single().Sql);
        }

        [Fact]
        public async Task GetCredentialHashAsync_ReturnsTheStoredHash()
        {
            var hash = Any.Bytes(32, seed: 3);
            _db.RowsFor = _ => new[] { new FakeRow().Set("credential_hash", hash) };

            Assert.Equal(hash, await _devices.GetCredentialHashAsync(_address));
        }

        [Fact]
        public async Task GetCredentialHashAsync_WithNoRow_ReturnsNull()
        {
            Assert.Null(await _devices.GetCredentialHashAsync(_address));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExistsAsync_ReturnsTheScalar(bool exists)
        {
            _db.ScalarFor = _ => exists;

            Assert.Equal(exists, await _devices.ExistsAsync(_address));
            Assert.Equal(SessionSetupSql.DeviceExists, _db.Single().Sql);
        }

        [Fact]
        public async Task TouchAsync_SendsTheUpdateThatMovesLastSeen()
        {
            await _devices.TouchAsync(_address);

            var command = _db.Single();
            Assert.Equal(SessionSetupSql.TouchDevice, command.Sql);
            Assert.Equal(_address.Account.Value, command.Parameter("@account_id"));
            Assert.Equal(2, command.Parameter("@device_id"));
        }

        [Fact]
        public async Task EveryDeviceStatementIsAddressedToOneDevice()
        {
            _db.ScalarFor = _ => true;

            await _devices.FindAsync(_address);
            await _devices.GetCredentialHashAsync(_address);
            await _devices.ExistsAsync(_address);
            await _devices.TouchAsync(_address);

            // An address is an account AND a device. A statement that binds only the account would
            // read or write every device a person owns. (EnsureAccount, LockAccount and NextDeviceId
            // are account-scoped on purpose and are excluded here.)
            Assert.All(_db.Executed, command =>
            {
                Assert.True(command.Bound("@account_id"));
                Assert.True(command.Bound("@device_id"));
            });
        }

        private FakeRow Row(int deviceId) => new FakeRow()
            .Set("account_id", _address.Account.Value)
            .Set("device_id", deviceId)
            .Set("display_name", $"device {deviceId}")
            .Set("registration_id", 1000 + deviceId)
            .Set("identity_algorithm", KeyAlgorithms.Ed25519)
            .Set("identity_key", Any.Bytes(32))
            .Set("credential_hash", Any.Bytes(32))
            .Set("registered_at", FixedClock.Default)
            .Set("last_seen_at", FixedClock.Default);
    }
}
