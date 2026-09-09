using Microsoft.OpenApi;

namespace SessionSetupMicroService.PostgresDB
{
    public class PostgresUnitOfWork : IUnitOfWork
    {
        public readonly PostgresConnectionScope _scope;
        public readonly ILogger<PostgresUnitOfWork> _logger;

        public PostgresUnitOfWork(PostgresConnectionScope scope, ILogger<PostgresUnitOfWork> logger)
        {
            _scope = scope;
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
        {
            
            if (_scope.InTransaction)
            {
                return await work(ct).ConfigureAwait(false);
            }

            await _scope.BeginAsync(ct).ConfigureAwait(false);
            try
            {
                var result = await work(ct).ConfigureAwait(false);
                await _scope.CommitAsync(ct).ConfigureAwait(false);
                return result;
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Rolling back unit of work");
                await _scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
    }
}
