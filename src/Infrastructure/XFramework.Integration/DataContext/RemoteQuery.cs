using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Integration.DataContext.ExpressionVisitor;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext;

public class RemoteQuery<T> : IRemoteQuery<T> where T : class
{
    private readonly HubConnection _connection;
    private readonly QueryDescriptor _descriptor;

    public RemoteQuery(HubConnection connection)
    {
        _connection = connection;
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

    public async Task<List<T>> ToListAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.ToList;
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<List<T>>((ReadOnlySpan<byte>)resultBytes) ?? [];
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.FirstOrDefault;
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<T?> SingleOrDefaultAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.SingleOrDefault;
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)resultBytes);
    }

    public async IAsyncEnumerable<T> ToAsyncEnumerable([EnumeratorCancellation] CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Stream;
        var descriptorBytes = MemoryPack.MemoryPackSerializer.Serialize(_descriptor);

        await foreach (var chunk in _connection.StreamAsync<byte[]>("StreamQuery", descriptorBytes, ct))
        {
            var item = MemoryPack.MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)chunk);
            if (item is not null)
                yield return item;
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Count;
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<int>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Any;
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<bool>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.AnyWithPredicate;
        _descriptor.PredicateFilters = QueryExpressionVisitor.Parse(predicate);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<bool>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.All;
        _descriptor.PredicateFilters = QueryExpressionVisitor.Parse(predicate);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<bool>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Min;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<TResult>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Max;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<TResult>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.MinBy;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(keySelector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.MaxBy;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(keySelector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<T>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Sum;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<decimal>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Average;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<double>((ReadOnlySpan<byte>)resultBytes);
    }

    public async Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.GroupBy;
        _descriptor.GroupByProperty = SortExpressionParser.GetPropertyName(keySelector);
        var resultBytes = await ExecuteAsync(ct);
        return MemoryPack.MemoryPackSerializer.Deserialize<List<GroupResult<TKey, T>>>((ReadOnlySpan<byte>)resultBytes) ?? [];
    }

    private async Task<byte[]> ExecuteAsync(CancellationToken ct)
    {
        var descriptorBytes = MemoryPack.MemoryPackSerializer.Serialize(_descriptor);
        return await _connection.InvokeAsync<byte[]>("ExecuteQuery", descriptorBytes, ct);
    }
}
