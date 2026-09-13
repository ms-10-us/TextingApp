using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using System.Collections.Concurrent;

namespace SessionSetupMicroService.Repositories
{
    public class InMemoryPreKeyRepository : IPreKeyRepository
    {
        private readonly InMemoryStore _store;

        public InMemoryPreKeyRepository(InMemoryStore store)
        {
            _store = store;
        }


        public Task<int> AddOneTimePreKeysAsync(ProtocolAddress address, IEnumerable<OneTimePreKey> keys, CancellationToken ct = default)
        {
            var added = 0;
            foreach(var group in keys.GroupBy(key => key.Kind))
            {
                var pool = _store.Pools.GetOrAdd(InMemoryStore.Key(address, group.Key), _ => new ConcurrentQueue<OneTimePreKey>());
                var present = pool.Select(key => key.Id.Value).ToHashSet();

                foreach(var key in group.Where(key => !present.Contains(key.Id.Value)))
                {
                    pool.Enqueue(key);
                    added++;
                }

            }

            return Task.FromResult(added);
        }

        public Task UpsertSignedPreKeyAsync(ProtocolAddress address, SignedPreKey key, CancellationToken ct = default)
        {
            _store.SignedPreKeys[InMemoryStore.Key(address, key.Kind)] = key;
            return Task.CompletedTask;
        }

        public Task<SignedPreKey?> GetSignedPreKeyAsync(ProtocolAddress address, PreKeyKind kind, CancellationToken ct = default)
        {
            return Task.FromResult(_store.SignedPreKeys.TryGetValue(InMemoryStore.Key(address, kind), out var key) ? key : null);
        }

        public Task<OneTimePreKey?> TakeOneTimePreKeyAsync(ProtocolAddress address, PreKeyKind kind, CancellationToken ct = default)
        {
            return Task.FromResult(_store.Pools.TryGetValue(InMemoryStore.Key(address, kind), out var pool) && pool.TryDequeue(out var key)
                ? key
                : null);
        }

        public Task<PreKeyInventory> CountAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            int Count(PreKeyKind kind) =>
                _store.Pools.TryGetValue(InMemoryStore.Key(address, kind), out var pool) ? pool.Count : 0;

            return Task.FromResult(new PreKeyInventory
            {
                Curve = Count(PreKeyKind.Curve),
                Kyber = Count(PreKeyKind.Kyber)
            });
        }
    }
}
