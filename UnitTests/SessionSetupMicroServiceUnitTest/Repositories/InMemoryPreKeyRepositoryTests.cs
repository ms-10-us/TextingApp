using System.Collections.Concurrent;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Tests.TestDoubles;
using SessionSetupMicroServiceUnitTest.TestDoubles;
using Xunit;

namespace SessionSetupMicroService.Tests.Repositories
{
    public class InMemoryPreKeyRepositoryTests
    {
        private readonly InMemoryStore _store = new();
        private readonly InMemoryPreKeyRepository _preKeys;

        public InMemoryPreKeyRepositoryTests()
        {
            _preKeys = new InMemoryPreKeyRepository(_store);
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_CountsPerKind()
        {
            var address = Any.Address();

            var added = await _preKeys.AddOneTimePreKeysAsync(address, new List<OneTimePreKey>
            {
                Any.OneTimeCurve(1), Any.OneTimeCurve(2), Any.OneTimeKyber(1)
            });

            Assert.Equal(3, added);

            var inventory = await _preKeys.CountAsync(address);
            Assert.Equal(2, inventory.Curve);
            Assert.Equal(1, inventory.Kyber);
        }

        [Fact]
        public async Task AddOneTimePreKeysAsync_IgnoresIdsAlreadyInThePool()
        {
            var address = Any.Address();
            await _preKeys.AddOneTimePreKeysAsync(address, new[] { Any.OneTimeCurve(1), Any.OneTimeCurve(2) });

            var added = await _preKeys.AddOneTimePreKeysAsync(address, new[] { Any.OneTimeCurve(2), Any.OneTimeCurve(3) });

            // A client that retries a timed-out upload re-sends the same ids. Converging beats
            // erroring, and beats duplicating.
            Assert.Equal(1, added);
            Assert.Equal(3, (await _preKeys.CountAsync(address)).Curve);
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_RemovesTheKeyItReturns()
        {
            var address = Any.Address();
            await _preKeys.AddOneTimePreKeysAsync(address, new[] { Any.OneTimeCurve(1) });

            var first = await _preKeys.TakeOneTimePreKeyAsync(address, PreKeyKind.Curve);
            var second = await _preKeys.TakeOneTimePreKeyAsync(address, PreKeyKind.Curve);

            Assert.NotNull(first);
            Assert.Equal(1, first!.Id.Value);
            Assert.Null(second);
            Assert.Equal(0, (await _preKeys.CountAsync(address)).Curve);
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_OnAnEmptyPool_ReturnsNullRatherThanThrowing()
        {
            // Null means "no row". It is the orchestrator, not the repository, that decides this
            // means the sender drops DH4.
            Assert.Null(await _preKeys.TakeOneTimePreKeyAsync(Any.Address(), PreKeyKind.Curve));
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_DoesNotCrossKinds()
        {
            var address = Any.Address();
            await _preKeys.AddOneTimePreKeysAsync(address, new[] { Any.OneTimeCurve(1) });

            Assert.Null(await _preKeys.TakeOneTimePreKeyAsync(address, PreKeyKind.Kyber));
            Assert.NotNull(await _preKeys.TakeOneTimePreKeyAsync(address, PreKeyKind.Curve));
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_DoesNotCrossDevices()
        {
            var account = Any.Account();
            var device1 = Any.AddressOn(account, 1);
            var device2 = Any.AddressOn(account, 2);

            await _preKeys.AddOneTimePreKeysAsync(device1, new[] { Any.OneTimeCurve(1) });

            Assert.Null(await _preKeys.TakeOneTimePreKeyAsync(device2, PreKeyKind.Curve));
        }

        [Fact]
        public async Task TakeOneTimePreKeyAsync_NeverIssuesTheSameKeyTwice()
        {
            var address = Any.Address();
            const int poolSize = 500;
            const int consumers = 25;

            await _preKeys.AddOneTimePreKeysAsync(
                address,
                Enumerable.Range(1, poolSize).Select(id => Any.OneTimeCurve(id)).ToList());

            var issued = new ConcurrentBag<long>();

            await Task.WhenAll(Enumerable.Range(0, consumers).Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < poolSize / consumers + 5; i++)
                {
                    var key = await _preKeys.TakeOneTimePreKeyAsync(address, PreKeyKind.Curve);
                    if (key is not null)
                    {
                        issued.Add(key.Id.Value);
                    }
                }
            })));

            // THE invariant. Issuing the same one-time prekey twice throws nothing and corrupts
            // nothing visible — it quietly weakens the forward secrecy of two conversations. Here it
            // holds because ConcurrentQueue.TryDequeue is atomic; in Postgres it holds because of
            // DELETE ... FOR UPDATE SKIP LOCKED ... RETURNING. This test passing says nothing about
            // that one: verify the real statement against a real database.
            Assert.Equal(poolSize, issued.Count);
            Assert.Equal(poolSize, issued.Distinct().Count());
            Assert.Equal(0, (await _preKeys.CountAsync(address)).Curve);
        }

        [Fact]
        public async Task UpsertSignedPreKeyAsync_ReplacesTheCurrentKeyOfThatKind()
        {
            var address = Any.Address();

            await _preKeys.UpsertSignedPreKeyAsync(address, Any.SignedCurvePreKey(keyId: 1));
            await _preKeys.UpsertSignedPreKeyAsync(address, Any.SignedCurvePreKey(keyId: 2));

            var current = await _preKeys.GetSignedPreKeyAsync(address, PreKeyKind.Curve);

            // Rotation is an upsert: a device has exactly one current signed prekey per kind.
            Assert.Equal(2, current!.Id.Value);
        }

        [Fact]
        public async Task GetSignedPreKeyAsync_ForAKindThatWasNeverPublished_ReturnsNull()
        {
            var address = Any.Address();
            await _preKeys.UpsertSignedPreKeyAsync(address, Any.SignedCurvePreKey());

            Assert.Null(await _preKeys.GetSignedPreKeyAsync(address, PreKeyKind.Kyber));
        }

        [Fact]
        public async Task CountAsync_ForADeviceWithNoPools_IsZero()
        {
            var inventory = await _preKeys.CountAsync(Any.Address());

            Assert.Equal(0, inventory.Curve);
            Assert.Equal(0, inventory.Kyber);
        }
    }
}
