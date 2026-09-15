using SessionSetupMicroService.Repositories;
using Xunit;

namespace SessionSetupMicroService.Tests.Repositories
{
    public class InMemoryUnitOfWorkTests
    {
        private readonly InMemoryStore _store = new();

        [Fact]
        public async Task ExecuteAsync_ReturnsWhateverTheWorkReturned()
        {
            var unitOfWork = new InMemoryUnitOfWork(_store);

            var result = await unitOfWork.ExecuteAsync(_ => Task.FromResult(42));

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ExecuteAsync_ReleasesTheGateWhenTheWorkThrows()
        {
            var unitOfWork = new InMemoryUnitOfWork(_store);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteAsync<int>(_ => throw new InvalidOperationException("boom")));

            // Without the finally, the semaphore is never released and every later request on this
            // provider hangs for ever — a deadlock that only shows up after the first failure.
            var after = await unitOfWork.ExecuteAsync(_ => Task.FromResult(1));
            Assert.Equal(1, after);
        }

        [Fact]
        public async Task ExecuteAsync_RunsOneUnitOfWorkAtATime()
        {
            var unitOfWork = new InMemoryUnitOfWork(_store);
            var concurrent = 0;
            var peak = 0;

            async Task<int> Work(CancellationToken ct)
            {
                var now = Interlocked.Increment(ref concurrent);
                InterlockedMax(ref peak, now);
                await Task.Delay(20, ct);
                Interlocked.Decrement(ref concurrent);
                return now;
            }

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => unitOfWork.ExecuteAsync(Work)));

            // This serialisation is the ONLY thing standing in for a database transaction here.
            // It is also why a green run against the in-memory provider says nothing about the
            // Postgres path: this lock lives in one process.
            Assert.Equal(1, peak);
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
