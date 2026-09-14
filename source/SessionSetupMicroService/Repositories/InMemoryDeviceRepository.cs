using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Repositories
{
    public class InMemoryDeviceRepository : IDeviceRepository
    {
        private readonly InMemoryStore _store;
        private readonly TimeProvider _timeProvider;

        public InMemoryDeviceRepository(InMemoryStore store, TimeProvider timeProvider)
        {
            _store = store;
            _timeProvider = timeProvider;
        }


        public Task EnsureAccountAsync(AccountId account, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<Device?> FindAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            return Task.FromResult(_store.Devices.TryGetValue(InMemoryStore.Key(address), out var device) ? device : null);
        }

        public Task<IEnumerable<Device>> ListByAccountAsync(AccountId account, CancellationToken ct = default)
        {
            return Task.FromResult<IEnumerable<Device>>(_store.Devices.Values
                .Where(device => device.Address.Account == account)
                .OrderBy(device => device.Address.Device.Value)
                .ToList());
        }

        public Task<byte[]?> GetCredentialHashAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            return Task.FromResult(_store.CredentialHashes.TryGetValue(
                InMemoryStore.Key(address), out var hash) ? hash : null);
        }

        public Task<bool> ExistsAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            return Task.FromResult(_store.Devices.ContainsKey(InMemoryStore.Key(address)));
        }

        public Task TouchAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            if (_store.Devices.TryGetValue(InMemoryStore.Key(address), out var device))
            {
                _store.Devices[InMemoryStore.Key(address)] = device with 
                { 
                    LastSeen = _timeProvider.GetUtcNow() 
                };
            }
                
            return Task.CompletedTask;
        }

        public Task<bool> InsertAsync(
           ProtocolAddress address,
           RegisterDeviceModel registration,
           byte[] credentialHash,
           CancellationToken ct = default)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            Device device = new Device
            {
                Address = address,
                DisplayName = registration.DisplayName,
                RegistrationId = registration.RegistrationId,
                IdentityKey = registration.IdentityKey,
                RegisteredAt = now,
                LastSeen = now
            };

            if (!_store.Devices.TryAdd(InMemoryStore.Key(address), device))
            {
                return Task.FromResult(false);
            }

            _store.CredentialHashes[InMemoryStore.Key(address)] = credentialHash;
            return Task.FromResult(true);
        }

        public Task<bool> LockAccountAsync(AccountId account, CancellationToken ct = default)
        {
            return Task.FromResult(_store.Devices.Values.Any(device => device.Address.Account == account));
        }

        public Task<DeviceId> NextDeviceIdAsync(AccountId account, CancellationToken ct = default)
        {
            var highest = _store.Devices.Values
                .Where(device => device.Address.Account == account)
                .Select(device => device.Address.Device.Value)
                .DefaultIfEmpty(0)
                .Max();

            return Task.FromResult(new DeviceId(highest + 1));
        }
    }
}
