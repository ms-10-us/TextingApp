using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Entities;
using UserMicroService.Models;
using UserMicroService.Repositories;

namespace UserMicroServiceUnitTests
{
    public sealed class InMemoryAuthRepository : IAuthRepository
    {
        private readonly Dictionary<Guid, string> _passwords = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, List<string>> _roles = new Dictionary<Guid, List<string>>();
        private readonly List<UserEntity> _users = new List<UserEntity>();

        public int MaxFailedAttempts { get; set; } = 3;

        public Dictionary<Guid, int> FailedAttempts { get;  } = new Dictionary<Guid, int>();

        public DateTime? LastSeenWritten { get; private set; }

        public void Seed(UserEntity user, string? password = null, params string[] roles)
        {
            _users.Add(user);
            if (password != null)
            {
                _passwords[user.Id] = password;
            }

            _roles[user.Id] = roles.ToList();
        }

        public Task<CredentialCheckResult> CheckPasswordAsync(UserEntity user, string password, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int failures = FailedAttempts.GetValueOrDefault(user.Id);
            if (failures >= MaxFailedAttempts)
            {
                return Task.FromResult(CredentialCheckResult.LockedOut);
            }

            if (_passwords.TryGetValue(user.Id, out string? stored) && stored == password)
            {
                FailedAttempts[user.Id] = 0;
                return Task.FromResult(CredentialCheckResult.Succeeded);
            }

            FailedAttempts[user.Id] = failures + 1;
            return Task.FromResult(FailedAttempts[user.Id] >= MaxFailedAttempts
                ? CredentialCheckResult.LockedOut
                : CredentialCheckResult.InvalidPassword);
        }

        public Task<UserEntity?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UserEntity? match = _users.SingleOrDefault(u =>
                string.Equals(u.Email, identifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.UserName, identifier, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(match);
        }

        public Task<IReadOnlyCollection<string>> GetRolesAsync(UserEntity user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyCollection<string> roles = _roles.GetValueOrDefault(user.Id, new List<string>()).ToArray();
            return Task.FromResult(roles);
        }

        public Task<bool> HasPasswordAsync(UserEntity user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_passwords.ContainsKey(user.Id));
        }

        public Task UpdateLastSeenAsync(UserEntity user, DateTime lastSeenUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.LastSeen = lastSeenUtc;
            LastSeenWritten = lastSeenUtc;
            return Task.CompletedTask;
        }
    }
}
