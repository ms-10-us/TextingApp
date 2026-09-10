using SessionSetupMicroService.Enums;
using SessionSetupMicroService.Models;
using System.Collections.Concurrent;

namespace SessionSetupMicroService.Repositories
{
    public class InMemoryStore
    {
        public readonly ConcurrentDictionary<string, Device> Devices = new ConcurrentDictionary<string, Device>();
        public readonly ConcurrentDictionary<string, byte[]> CredentialHashes = new ConcurrentDictionary<string, byte[]>();
        public readonly ConcurrentDictionary<string, SignedPreKey> SignedPreKeys = new ConcurrentDictionary<string, SignedPreKey>();
        public readonly ConcurrentDictionary<string, ConcurrentQueue<OneTimePreKey>> Pools = 
            new ConcurrentDictionary<string, ConcurrentQueue<OneTimePreKey>>();
        public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        internal static string Key(ProtocolAddress address) => address.ToString();
        internal static string Key(ProtocolAddress address, PreKeyKind kind) => $"{address}:{kind}";
    }
}
