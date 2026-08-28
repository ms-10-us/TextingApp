using System;
using System.Collections.Generic;
using System.Text;
using UserMicroService.Entities;
using UserMicroService.Models;
using UserMicroService.Repositories;

namespace UserMicroServiceUnitTests
{
    public sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<UserEntity> _users = new List<UserEntity>();

        public IReadOnlyList<UserEntity> Users => _users;

        public OperationResult<UserEntity>? NextCreateResult { get; set; }

        public string? LastPassword { get;  private set; }

        public void Seed(params UserEntity[] users) => _users.AddRange(users);

        public Task<OperationResult<UserEntity>> CreateAsync(UserEntity user, string password, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPassword = password;

            if (NextCreateResult != null)
            {
                return Task.FromResult(NextCreateResult);
            }

            _users.Add(user);
            return Task.FromResult(OperationResult<UserEntity>.Success(user));
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<UserEntity?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_users.SingleOrDefault(u => u.Id == userId));
        }

        public Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_users.Any(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
