using SessionSetupMicroService.PostgresDB;

namespace SessionSetupMicroService.Repositories
{
    public class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
