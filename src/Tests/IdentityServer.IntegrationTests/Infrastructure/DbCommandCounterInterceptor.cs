using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityServer.IntegrationTests.Infrastructure;

internal sealed class DbCommandCounterInterceptor : DbCommandInterceptor
{
    private readonly AsyncLocal<Counter?> _activeCounter = new();

    public Measurement BeginMeasurement()
    {
        if (_activeCounter.Value is not null)
            throw new InvalidOperationException("A database command measurement is already active.");

        var counter = new Counter();
        _activeCounter.Value = counter;
        return new Measurement(this, counter);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return ValueTask.FromResult(result);
    }

    private void Increment()
    {
        if (_activeCounter.Value is { } counter)
            Interlocked.Increment(ref counter.Count);
    }

    private void EndMeasurement(Counter counter)
    {
        if (ReferenceEquals(_activeCounter.Value, counter))
            _activeCounter.Value = null;
    }

    internal sealed class Counter
    {
        public int Count;
    }

    internal sealed class Measurement(DbCommandCounterInterceptor owner, Counter counter) : IDisposable
    {
        private bool _disposed;

        public int CommandCount => Volatile.Read(ref counter.Count);

        public void Dispose()
        {
            if (_disposed)
                return;

            owner.EndMeasurement(counter);
            _disposed = true;
        }
    }
}
