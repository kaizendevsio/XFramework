using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using XFramework.Integration.DataContext.ExpressionVisitor;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext;

/// <summary>
/// Remote query implementation that proxies EF Core queries through the Bolt hub.
/// DB proxy migration to the Bolt thin protocol is parked work (see Task 14).
/// This class retains the QueryDescriptor building logic so CachingQuery can still
/// inspect it for cache-key construction; execution methods throw NotImplementedException.
/// </summary>
public class RemoteQuery<T> : IRemoteQuery<T> where T : class
{
    private readonly QueryDescriptor _descriptor;

    private const string PendingMigrationMessage =
        "DB proxy migration to Bolt thin protocol is pending — see DB proxy decentralization parked work (Task 14).";

    public RemoteQuery()
    {
        _descriptor = new QueryDescriptor
        {
            EntityTypeName = typeof(T).Name
        };
    }

    internal QueryDescriptor Descriptor => _descriptor;

    public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        var filters = QueryExpressionVisitor.Parse(predicate);
        _descriptor.Filters.AddRange(filters);
        return this;
    }

    public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _descriptor.Sorting.Add(new SortDescriptor
        {
            PropertyName = SortExpressionParser.GetPropertyName(keySelector),
            Descending = false,
            IsSecondary = false
        });
        return this;
    }

    public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _descriptor.Sorting.Add(new SortDescriptor
        {
            PropertyName = SortExpressionParser.GetPropertyName(keySelector),
            Descending = true,
            IsSecondary = false
        });
        return this;
    }

    public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _descriptor.Sorting.Add(new SortDescriptor
        {
            PropertyName = SortExpressionParser.GetPropertyName(keySelector),
            Descending = false,
            IsSecondary = true
        });
        return this;
    }

    public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _descriptor.Sorting.Add(new SortDescriptor
        {
            PropertyName = SortExpressionParser.GetPropertyName(keySelector),
            Descending = true,
            IsSecondary = true
        });
        return this;
    }

    public IRemoteQuery<T> Skip(int count)
    {
        _descriptor.Skip = count;
        return this;
    }

    public IRemoteQuery<T> Take(int count)
    {
        _descriptor.Take = count;
        return this;
    }

    public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector)
    {
        _descriptor.Includes.Add(MemberAccessParser.GetPropertyPath(navigationSelector.Body));
        return this;
    }

    public IRemoteQuery<T> Distinct()
    {
        _descriptor.ApplyDistinct = true;
        return this;
    }

    public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _descriptor.DistinctByProperty = SortExpressionParser.GetPropertyName(keySelector);
        return this;
    }

    public IRemoteQuery<T> NoCache()
    {
        _descriptor.NoCache = true;
        return this;
    }

    public Task<List<T>> ToListAsync(CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public async IAsyncEnumerable<T> ToAsyncEnumerable([EnumeratorCancellation] CancellationToken ct = default)
    {
        throw new NotImplementedException(PendingMigrationMessage);
        yield break; // Unreachable — satisfies compiler for IAsyncEnumerable
    }

    public Task<int> CountAsync(CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);

    public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken ct = default)
        => throw new NotImplementedException(PendingMigrationMessage);
}
