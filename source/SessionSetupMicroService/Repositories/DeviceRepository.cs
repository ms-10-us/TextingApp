using Microsoft.OpenApi;
using SessionSetupMicroService.Entities;
using SessionSetupMicroService.ExtensionMethods;
using SessionSetupMicroService.Mapping;
using SessionSetupMicroService.Models;
using SessionSetupMicroService.PostgresDB;
using System.Data.Common;

namespace SessionSetupMicroService.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        public required PostgresConnectionScope _scope;

        public DeviceRepository(PostgresConnectionScope scope)
        {
            _scope = scope;
        }

        public async Task EnsureAccountAsync(AccountId account, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.EnsureAccount, ct);
            command.Bind("@account_id", account.Value);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(
            ProtocolAddress address, 
            RegisterDeviceModel registration, 
            byte[] credentialHash, 
            CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.InsertDevice, ct);
            BindAddress(command, address);
            command
                .Bind("@display_name", registration.DisplayName)
                .Bind("@registration_id", registration.RegistrationId.Value)
                .Bind("@identity_algorithm", registration.IdentityKey.Algorithm)
                .Bind("@identity_key", registration.IdentityKey.Value)
                .Bind("@credential_hash", credentialHash);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<Device?> FindAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.FindDevice, ct);
            BindAddress (command, address);

            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadDevice(reader).ToModel() : null;
        }

        public async Task<IEnumerable<Device>> ListByAccountAsync(AccountId account, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.ListDevicesByAccount, ct);
            command.Bind("@account_id", account.Value);

            var devices = new List<Device>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) devices.Add(ReadDevice(reader).ToModel());
            return devices;
        }

        public async Task<byte[]?> GetCredentialHashAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.GetCredentialHash, ct);
            BindAddress(command, address);

            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? reader.GetBytes("credential_hash") : null;
        }

        public async Task<bool> ExistsAsync(ProtocolAddress address, CancellationToken ct = default)
        {
            await using var command = await _scope.CreateCommandAsync(SessionSetupSql.DeviceExists, ct);
            BindAddress(command, address);
            return await command.ExecuteScalarAsync(ct) is true;
        }

        private static void BindAddress(DbCommand command, ProtocolAddress address) =>
            command.Bind("@account_id", address.Account.Value).Bind("@device_id", address.Device.Value);

        private static DeviceEntity ReadDevice(DbDataReader reader)
        {
            return new DeviceEntity
            {
                AccountId = reader.GetGuid("account_id"),
                DeviceId = reader.GetInt("device_id"),
                DisplayName = reader.GetText("display_name"),
                RegistrationId = reader.GetInt("registration_id"),
                IdentityAlgorithm = reader.GetText("identity_algorithm"),
                IdentityKey = reader.GetBytes("identity_key"),
                CredentialHash = reader.GetBytes("credential_hash"),
                RegisteredAt = reader.GetTimestamp("registered_at"),
                LastSeenAt = reader.GetTimestamp("last_seen_at"),
            };
        }
    }
}
