using SessionSetupMicroService.Models;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Repositories
{
    public class InMemoryDeviceRepositoryTests
    {
        private readonly InMemoryStore _store = new();
        private readonly FixedClock _clock = new();
        private readonly InMemoryDeviceRepository _devices;

        public InMemoryDeviceRepositoryTests()
        {
            _devices = new InMemoryDeviceRepository(_store, _clock);
        }

        [Fact]
        public async Task InsertAsync_ThenFindAsync_RoundTripsTheDevice()
        {
            var address = Any.Address();
            var registration = Any.Registration(displayName: "Mazen laptop", registrationId: 1234);

            Assert.True(await _devices.InsertAsync(address, registration, Any.Bytes(32), CancellationToken.None));

            var found = await _devices.FindAsync(address);

            Assert.NotNull(found);
            Assert.Equal(address, found!.Address);
            Assert.Equal("Mazen laptop", found.DisplayName);
            Assert.Equal(1234, found.RegistrationId.Value);
            Assert.Equal(_clock.GetUtcNow(), found.RegisteredAt);
            Assert.Equal(_clock.GetUtcNow(), found.LastSeen);
        }

        [Fact]
        public async Task FindAsync_ForAnUnknownAddress_ReturnsNull()
        {
            Assert.Null(await _devices.FindAsync(Any.Address()));
        }

        [Fact]
        public async Task InsertAsync_AtAnAddressThatIsTaken_ReturnsFalseAndLeavesTheOriginal()
        {
            var address = Any.Address();
            await _devices.InsertAsync(address, Any.Registration(displayName: "first"), Any.Bytes(32));

            var second = await _devices.InsertAsync(address, Any.Registration(displayName: "second"), Any.Bytes(32));

            // A duplicate is reported, not thrown — same contract as the Postgres repository, which
            // reports it through the row count of `on conflict do nothing`. The caller decides what
            // a duplicate means.
            Assert.False(second);
            Assert.Equal("first", (await _devices.FindAsync(address))!.DisplayName);
        }

        [Fact]
        public async Task LockAccountAsync_IsFalseForAnAccountWithNoDevices()
        {
            // No lock to take here — InMemoryUnitOfWork serialises everything behind one semaphore.
            // The return value only distinguishes an existing account from one that never existed.
            Assert.False(await _devices.LockAccountAsync(Any.Account()));
        }

        [Fact]
        public async Task LockAccountAsync_IsTrueOnceTheAccountHasADevice()
        {
            var account = Any.Account();
            await _devices.InsertAsync(Any.AddressOn(account, 1), Any.Registration(), Any.Bytes(32));

            Assert.True(await _devices.LockAccountAsync(account));
            Assert.False(await _devices.LockAccountAsync(Any.Account()));
        }

        [Fact]
        public async Task NextDeviceIdAsync_ForAnEmptyAccount_IsOne()
        {
            var next = await _devices.NextDeviceIdAsync(Any.Account());

            Assert.Equal(DeviceId.Primary, next.Value);
        }

        [Fact]
        public async Task NextDeviceIdAsync_IsOneAboveTheHighestIdOnThatAccount()
        {
            var account = Any.Account();
            await _devices.InsertAsync(Any.AddressOn(account, 1), Any.Registration(), Any.Bytes(32));
            await _devices.InsertAsync(Any.AddressOn(account, 4), Any.Registration(), Any.Bytes(32));

            // Highest + 1, not count + 1: ids are never reused, so a gap left by an unlinked device
            // stays a gap.
            Assert.Equal(5, (await _devices.NextDeviceIdAsync(account)).Value);
        }

        [Fact]
        public async Task NextDeviceIdAsync_IgnoresOtherAccounts()
        {
            var mine = Any.Account();
            var theirs = Any.Account();
            await _devices.InsertAsync(Any.AddressOn(theirs, 9), Any.Registration(), Any.Bytes(32));

            Assert.Equal(DeviceId.Primary, (await _devices.NextDeviceIdAsync(mine)).Value);
        }

        [Fact]
        public async Task ListByAccountAsync_ReturnsOnlyThatAccountsDevices_OrderedByDeviceId()
        {
            var mine = Any.Account();
            var theirs = Any.Account();

            await _devices.InsertAsync(Any.AddressOn(mine, 3), Any.Registration(), Any.Bytes(32));
            await _devices.InsertAsync(Any.AddressOn(mine, 1), Any.Registration(), Any.Bytes(32));
            await _devices.InsertAsync(Any.AddressOn(theirs, 1), Any.Registration(), Any.Bytes(32));

            var listed = (await _devices.ListByAccountAsync(mine)).ToList();

            Assert.Equal(2, listed.Count);
            Assert.Equal(new[] { 1, 3 }, listed.Select(device => device.Address.Device.Value));
        }

        [Fact]
        public async Task ExistsAsync_ReflectsWhetherTheDeviceWasInserted()
        {
            var address = Any.Address();

            Assert.False(await _devices.ExistsAsync(address));
            await _devices.InsertAsync(address, Any.Registration(), Any.Bytes(32));
            Assert.True(await _devices.ExistsAsync(address));
        }

        [Fact]
        public async Task GetCredentialHashAsync_ReturnsWhatInsertStored()
        {
            var address = Any.Address();
            var hash = Any.Bytes(32, seed: 7);

            await _devices.InsertAsync(address, Any.Registration(), hash);

            Assert.Equal(hash, await _devices.GetCredentialHashAsync(address));
            Assert.Null(await _devices.GetCredentialHashAsync(Any.Address(deviceId: 9)));
        }

        [Fact]
        public async Task TouchAsync_MovesLastSeenAndLeavesRegisteredAtAlone()
        {
            var address = Any.Address();
            await _devices.InsertAsync(address, Any.Registration(), Any.Bytes(32));
            var registeredAt = (await _devices.FindAsync(address))!.RegisteredAt;

            _clock.Advance(TimeSpan.FromHours(3));
            await _devices.TouchAsync(address);

            var touched = await _devices.FindAsync(address);
            Assert.Equal(registeredAt, touched!.RegisteredAt);
            Assert.Equal(registeredAt.AddHours(3), touched.LastSeen);
        }

        [Fact]
        public async Task TouchAsync_ForAnUnknownAddress_DoesNothing()
        {
            await _devices.TouchAsync(Any.Address());
            Assert.Empty(_store.Devices);
        }
    }
}
