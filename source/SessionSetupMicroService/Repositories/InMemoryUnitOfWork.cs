using SessionSetupMicroService.PostgresDB;

namespace SessionSetupMicroService.Repositories
{
    public class InMemoryUnitOfWork : IUnitOfWork
    {
        private readonly InMemoryStore _store;

        public InMemoryUnitOfWork(InMemoryStore store)
        {
            _store = store;
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
        {
            await _store.Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await work(ct).ConfigureAwait(false);
            }
            finally
            {
                _store.Gate.Release();
            }
        }
    }
}
