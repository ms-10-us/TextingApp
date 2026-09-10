using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Repositories
{
    public interface IPreKeyRepository
    {
        Task UpsertSignedPreKeyAsync(ProtocolAddress address, SignedPreKey key, CancellationToken ct = default);

        Task<int> AddOneTimePreKeysAsync(ProtocolAddress address, IEnumerable<OneTimePreKey> keys, CancellationToken ct = default);
    }
}
