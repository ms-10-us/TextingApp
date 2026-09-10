using SessionSetupMicroService.ExtensionMethods;
using SessionSetupMicroService.Mapping;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.PostgresDB;
using System.Data.Common;

namespace SessionSetupMicroService.Repositories
{
    public class PreKeyRepository : IPreKeyRepository
    {
        public required PostgresConnectionScope _scope;

        public PreKeyRepository(PostgresConnectionScope scope)
        {
            _scope = scope;
        }

        public async Task UpsertSignedPreKeyAsync(ProtocolAddress address, SignedPreKey key, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.UpsertSignedPreKey, ct);
            BindAddress(command, address);
            command
                 .Bind("@kind", key.Kind.ToEntity())
                 .Bind("@key_id", key.Id.Value)
                 .Bind("@public_key", key.PublicKey.Value)
                 .Bind("@signature", key.Signature);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> AddOneTimePreKeysAsync(
            ProtocolAddress address, 
            IEnumerable<OneTimePreKey> keys, 
            CancellationToken ct = default)
        {
            if (keys.Count() == 0)
            {
                return 0;
            }

            int inserted = 0;

            foreach(var group in keys.GroupBy(key => key.Kind))
            {
                var batch = group.ToList();
                await using var command = await _scope.CreateCommandAsync(SessionSetupSql.AddOneTimePreKeys, ct);
                BindAddress(command, address);
                command
                    .Bind("@kind", group.Key.ToEntity())
                    .Bind("@key_ids", batch.Select(key => key.Id.Value).ToArray())
                    .Bind("@public_keys", batch.Select(key => key.PublicKey.Value).ToArray())
                    .Bind("@signatures", batch.Select(key => key.Signature ?? []).ToArray());

                inserted += await command.ExecuteNonQueryAsync(ct);
            }

            return inserted;
        }




        private static void BindAddress(DbCommand command, ProtocolAddress address) =>
            command.Bind("@account_id", address.Account.Value).Bind("@device_id", address.Device.Value);

        
    }
}
