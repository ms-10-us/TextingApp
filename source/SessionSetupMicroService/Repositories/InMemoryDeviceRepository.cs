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

        public Task InsertAsync(ProtocolAddress address, RegisterDeviceModel registration, byte[] credentialHash, CancellationToken ct = default)
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
                throw new InvalidOperationException($"Device {address} already exists.");
            }

            _store.CredentialHashes[InMemoryStore.Key(address)] = credentialHash;
            return Task.CompletedTask;
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
    }
}
