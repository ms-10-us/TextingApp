using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;

namespace SessionSetupMicroService.Repositories
{
    public interface IPreKeyRepository
    {
        Task UpsertSignedPreKeyAsync(ProtocolAddress address, SignedPreKey key, CancellationToken ct = default);

        Task<int> AddOneTimePreKeysAsync(ProtocolAddress address, IEnumerable<OneTimePreKey> keys, CancellationToken ct = default);

        Task<SignedPreKey?> GetSignedPreKeyAsync(ProtocolAddress address, PreKeyKind kind, CancellationToken ct = default);

        Task<OneTimePreKey?> TakeOneTimePreKeyAsync(ProtocolAddress address, PreKeyKind kind, CancellationToken ct = default);

        Task<PreKeyInventory> CountAsync(ProtocolAddress address, CancellationToken ct = default);
    }
}
