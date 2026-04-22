using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public class ServerQuery<T> : IRemoteQuery<T> where T : class
{
    private IQueryable<T> _queryable;
    private bool _isOrdered;

    public ServerQuery(IQueryable<T> queryable)
    {
        _queryable = queryable.AsNoTracking();
    }

    public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        _queryable = _queryable.Where(predicate);
        return this;
    }

    public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _queryable = _queryable.OrderBy(keySelector);
        _isOrdered = true;
        return this;
    }

    public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _queryable = _queryable.OrderByDescending(keySelector);
        _isOrdered = true;
        return this;
    }

    public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (!_isOrdered)
            throw new InvalidOperationException("ThenBy can only be called after OrderBy or OrderByDescending.");
        _queryable = ((IOrderedQueryable<T>)_queryable).ThenBy(keySelector);
        return this;
    }

    public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (!_isOrdered)
            throw new InvalidOperationException("ThenByDescending can only be called after OrderBy or OrderByDescending.");
        _queryable = ((IOrderedQueryable<T>)_queryable).ThenByDescending(keySelector);
        return this;
    }

    public IRemoteQuery<T> Skip(int count)
    {
        _queryable = _queryable.Skip(count);
        return this;
    }

    public IRemoteQuery<T> Take(int count)
    {
        _queryable = _queryable.Take(count);
        return this;
    }

    public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector)
    {
        _queryable = _queryable.Include(navigationSelector);
        return this;
    }

    public IRemoteQuery<T> Distinct()
    {
        _queryable = _queryable.Distinct();
        return this;
    }

    public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        _queryable = _queryable.DistinctBy(keySelector);
        return this;
    }

    public IRemoteQuery<T> NoCache()
    {
        // No-op on server — no client-side cache to bypass
        return this;
    }

    public Task<List<T>> ToListAsync(CancellationToken ct = default)
        => _queryable.ToListAsync(ct);

    public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
        => _queryable.FirstOrDefaultAsync(ct);

    public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default)
        => _queryable.SingleOrDefaultAsync(ct);

    public async IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in _queryable.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return item;
        }
    }

    public Task<int> CountAsync(CancellationToken ct = default)
        => _queryable.CountAsync(ct);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _queryable.AnyAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _queryable.AnyAsync(predicate, ct);

    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _queryable.AllAsync(predicate, ct);

    public async Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await _queryable.MinAsync(selector, ct);

    public async Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await _queryable.MaxAsync(selector, ct);

    public async Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
        => await _queryable.OrderBy(keySelector).FirstOrDefaultAsync(ct);

    public async Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
        => await _queryable.OrderByDescending(keySelector).FirstOrDefaultAsync(ct);

    public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
        => _queryable.SumAsync(selector, ct);

    public async Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
        => (double)await _queryable.AverageAsync(selector, ct);

    public async Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken ct = default)
    {
        return await _queryable
            .GroupBy(keySelector)
            .Select(g => new GroupResult<TKey, T>
            {
                Key = g.Key,
                Items = g.ToList()
            })
            .ToListAsync(ct);
    }
}
