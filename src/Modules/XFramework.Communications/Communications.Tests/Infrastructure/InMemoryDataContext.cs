using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using XFramework.Domain.Shared.DataContext;

namespace Communications.Tests.Infrastructure;

internal sealed class InMemoryDataContext : IDataContext
{
    private readonly Dictionary<Type, IList<object>> _sets = [];

    public List<T> Set<T>() where T : class
    {
        var type = typeof(T);
        if (!_sets.TryGetValue(type, out var set))
        {
            set = new List<object>();
            _sets[type] = set;
        }

        return set.Cast<T>().ToList();
    }

    public void Seed<T>(params T[] entities) where T : class
    {
        foreach (var entity in entities)
            Add(entity);
    }

    public void Seed(params object[] entities)
    {
        foreach (var entity in entities)
        {
            var type = entity.GetType();
            if (!_sets.TryGetValue(type, out var set))
            {
                set = new List<object>();
                _sets[type] = set;
            }

            set.Add(entity);
        }
    }

    public IRemoteQuery<T> Query<T>() where T : class => new InMemoryRemoteQuery<T>(Set<T>().AsQueryable());

    public void Add<T>(T entity) where T : class
    {
        var type = typeof(T);
        if (!_sets.TryGetValue(type, out var set))
        {
            set = new List<object>();
            _sets[type] = set;
        }

        set.Add(entity);
    }

    public void Update<T>(T entity) where T : class
    {
    }

    public void Remove<T>(T entity) where T : class
    {
        if (_sets.TryGetValue(typeof(T), out var set))
            set.Remove(entity);
    }

    public Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default) =>
        Task.FromResult(DataContextResult.Success());
}

internal sealed class InMemoryRemoteQuery<T>(IQueryable<T> queryable) : IRemoteQuery<T>
    where T : class
{
    private IQueryable<T> _queryable = queryable;
    private bool _isOrdered;

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

    public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector) => this;

    public IRemoteQuery<T> Distinct()
    {
        _queryable = _queryable.Distinct();
        return this;
    }

    public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        var key = keySelector.Compile();
        _queryable = _queryable.AsEnumerable().DistinctBy(key).AsQueryable();
        return this;
    }

    public IRemoteQuery<T> NoCache() => this;

    public IRemoteQuery<T> IgnoreQueryFilters() => this;

    public Task<List<T>> ToListAsync(CancellationToken ct = default) =>
        Task.FromResult(_queryable.ToList());

    public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default) =>
        Task.FromResult(_queryable.FirstOrDefault());

    public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default) =>
        Task.FromResult(_queryable.SingleOrDefault());

    public async IAsyncEnumerable<T> ToAsyncEnumerable(
        int chunkSize = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in _queryable)
            yield return item;

        await Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        Task.FromResult(_queryable.Count());

    public Task<bool> AnyAsync(CancellationToken ct = default) =>
        Task.FromResult(_queryable.Any());

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(_queryable.Any(predicate));

    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(_queryable.All(predicate));

    public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
        Task.FromResult(_queryable.Min(selector));

    public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
        Task.FromResult(_queryable.Max(selector));

    public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
        Task.FromResult(_queryable.AsEnumerable().MinBy(keySelector.Compile()));

    public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
        Task.FromResult(_queryable.AsEnumerable().MaxBy(keySelector.Compile()));

    public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
        Task.FromResult(_queryable.Sum(selector));

    public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
        Task.FromResult((double)_queryable.Average(selector));

    public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken ct = default)
    {
        var key = keySelector.Compile();
        var groups = _queryable
            .AsEnumerable()
            .GroupBy(key)
            .Select(g => new GroupResult<TKey, T>
            {
                Key = g.Key,
                Items = g.ToList()
            })
            .ToList();

        return Task.FromResult(groups);
    }
}
