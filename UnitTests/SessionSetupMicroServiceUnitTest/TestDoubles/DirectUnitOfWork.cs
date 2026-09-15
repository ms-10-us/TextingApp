using SessionSetupMicroService.PostgresDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace SessionSetupMicroServiceUnitTest.TestDoubles
{
    internal class DirectUnitOfWork : IUnitOfWork
    {
        public int Executions { get; private set; }

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
        {
            Executions++;
            return work(ct);
        }
    }
}
