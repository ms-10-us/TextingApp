namespace SessionSetupMicroService.PostgresDB
{
    public interface IUnitOfWork
    {
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
    }
}
