using System.Linq.Expressions;

namespace XFramework.Domain.Shared.DataContext;

public interface IRemoteQuery<T> where T : class
{
    // Filtering
    IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate);

    // Sorting
    IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);
    IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
    IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector);
    IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector);

    // Pagination
    IRemoteQuery<T> Skip(int count);
    IRemoteQuery<T> Take(int count);

    // Navigation
    IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector);

    // Deduplication
    IRemoteQuery<T> Distinct();
    IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector);

    // Caching control
    IRemoteQuery<T> NoCache();

    // Terminal: materialization
    Task<List<T>> ToListAsync(CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken ct = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken ct = default);
    IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, CancellationToken ct = default);

    // Terminal: scalar
    Task<int> CountAsync(CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // Terminal: aggregation
    Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default);
    Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default);
    Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default);
    Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default);

    // Terminal: grouping
    Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken ct = default);
}
