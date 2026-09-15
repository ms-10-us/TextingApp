using NSubstitute.Exceptions;
using SessionSetupMicroService.PostgresDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace SessionSetupMicroServiceUnitTest.TestDoubles
{
    public class OrderRecordingUnitOfWork : IUnitOfWork
    {
        public List<string> Calls { get; } = new List<string>();

        public void Record(string step) => Calls.Add(step);

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
        {
            Calls.Add("BEGIN");
            try
            {
                return await work(ct);
            }
            finally
            {
                Calls.Add("COMMIT");
            }
        }
    }
}
