using SessionSetupMicroService.Entities;
using SessionSetupMicroService.Enums;
using SessionSetupMicroService.ExtensionMethods;
using SessionSetupMicroService.Mapping;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.PostgresDB;
using System.Data.Common;

namespace SessionSetupMicroService.Repositories
{
    public class PreKeyRepository : IPreKeyRepository
    {
        public readonly PostgresConnectionScope _scope;

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

        public async Task<SignedPreKey?> GetSignedPreKeyAsync(
            ProtocolAddress address, 
            PreKeyKind kind, 
            CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.GetSignedPreKey, ct);
            BindAddress(command, address);
            command.Bind("@kind", kind.ToEntity());


            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return new SignedPreKeyEntity
            {
                AccountId = reader.GetGuid("account_id"),
                DeviceId = reader.GetInt("device_id"),
                Kind = reader.GetText("kind"),
                KeyId = reader.GetLong("key_id"),
                PublicKey = reader.GetBytes("public_key"),
                Signature = reader.GetBytes("signature"),
                CreatedAt = reader.GetTimestamp("created_at"),
            }.ToModel();
        }

        public async Task<OneTimePreKey?> TakeOneTimePreKeyAsync(ProtocolAddress address, PreKeyKind kind, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.TakeOneTimePreKey, ct);
            BindAddress(command, address);
            command.Bind("@kind", kind.ToEntity());

            await using var reader = await command.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return new OneTimePreKeyEntity
            {
                AccountId = reader.GetGuid("account_id"),
                DeviceId = reader.GetInt("device_id"),
                Kind = reader.GetText("kind"),
                KeyId = reader.GetLong("key_id"),
                PublicKey = reader.GetBytes("public_key"),
                Signature = reader.GetNullableBytes("signature"),
                CreatedAt = reader.GetTimestamp("created_at"),
            }.ToModel();
        }

        public async Task<PreKeyInventory> CountAsync(ProtocolAddress address, CancellationToken ct = default)
        {

            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.CountOneTimePreKeys, ct);
            BindAddress(command, address);

            var curve = 0;
            var kyber = 0;

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var count = (int)reader.GetInt64(1);
                if (SessionSetupMapping.ToModel(reader.GetString(0)) == PreKeyKind.Curve)
                {
                    curve = count;
                }
                else
                {
                    kyber = count;
                }
            }

            return new PreKeyInventory
            {
                Curve = curve,
                Kyber = kyber
            };
        }

        private static void BindAddress(DbCommand command, ProtocolAddress address) =>
            command.Bind("@account_id", address.Account.Value).Bind("@device_id", address.Device.Value);
    }
}
