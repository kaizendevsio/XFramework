using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.ExpressionVisitor;

namespace XFramework.Integration.DataContext;

public class RemoteQuery<T> : IRemoteQuery<T> where T : class
{
    private readonly QueryDescriptor _descriptor;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<TrackedEntity> _trackedEntities;
    private readonly RequestMetadata? _metadata;

    public RemoteQuery(
        IServiceProvider serviceProvider,
        List<TrackedEntity> trackedEntities,
        RequestMetadata? metadata)
    {
        _serviceProvider = serviceProvider;
        _trackedEntities = trackedEntities;
        _metadata = metadata;
        _descriptor = new QueryDescriptor { EntityTypeName = typeof(T).Name };
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

    // Terminal: materialization

    public async Task<List<T>> ToListAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.ToList;
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<List<T>>(resultBytes);
        if (result is not null)
            foreach (var entity in result) TrackEntity(entity);
        return result ?? [];
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.FirstOrDefault;
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<T?>(resultBytes);
        if (result is not null) TrackEntity(result);
        return result;
    }

    public async Task<T?> SingleOrDefaultAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.SingleOrDefault;
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<T?>(resultBytes);
        if (result is not null) TrackEntity(result);
        return result;
    }

    // Terminal: streaming

    public async IAsyncEnumerable<T> ToAsyncEnumerable(
        int chunkSize = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Stream;
        _descriptor.ChunkSize = chunkSize;
        _descriptor.Metadata = _metadata;

        var wrapper = ResolveWrapper();
        var descriptorBytes = MemoryPackSerializer.Serialize(_descriptor);

        await foreach (var chunkBytes in wrapper.ExecuteQueryStreamAsync(descriptorBytes, ct))
        {
            var entityBytesList = MemoryPackSerializer.Deserialize<List<byte[]>>(chunkBytes);
            if (entityBytesList is null) continue;
            foreach (var entityBytes in entityBytesList)
            {
                var entity = MemoryPackSerializer.Deserialize<T>(entityBytes);
                if (entity is not null)
                {
                    TrackEntity(entity);
                    yield return entity;
                }
            }
        }
    }

    // Terminal: scalar

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Count;
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<int>(resultBytes);
    }

    public async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Any;
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<bool>(resultBytes);
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.AnyWithPredicate;
        _descriptor.PredicateFilters = QueryExpressionVisitor.Parse(predicate);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<bool>(resultBytes);
    }

    public async Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.All;
        _descriptor.PredicateFilters = QueryExpressionVisitor.Parse(predicate);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<bool>(resultBytes);
    }

    // Terminal: aggregation

    public async Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Min;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<TResult?>(resultBytes);
    }

    public async Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Max;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<TResult?>(resultBytes);
    }

    public async Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.MinBy;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(keySelector);
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<T?>(resultBytes);
        if (result is not null) TrackEntity(result);
        return result;
    }

    public async Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.MaxBy;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(keySelector);
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<T?>(resultBytes);
        if (result is not null) TrackEntity(result);
        return result;
    }

    public async Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Sum;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<decimal>(resultBytes);
    }

    public async Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.Average;
        _descriptor.AggregateProperty = SortExpressionParser.GetPropertyName(selector);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<double>(resultBytes);
    }

    public async Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
        Expression<Func<T, TKey>> keySelector,
        CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.GroupBy;
        _descriptor.GroupByProperty = SortExpressionParser.GetPropertyName(keySelector);
        var resultBytes = await ExecuteQueryAsync(ct);
        return MemoryPackSerializer.Deserialize<List<GroupResult<TKey, T>>>(resultBytes) ?? [];
    }

    // Helpers

    private async Task<byte[]> ExecuteQueryAsync(CancellationToken ct)
    {
        _descriptor.Metadata = _metadata;
        var wrapper = ResolveWrapper();
        var descriptorBytes = MemoryPackSerializer.Serialize(_descriptor);
        return await wrapper.ExecuteQueryAsync(descriptorBytes, ct);
    }

    private IDataContextServiceWrapper ResolveWrapper()
    {
        var wrapperMap = RemoteDataContext.GetServiceWrapperMap();
        if (!wrapperMap.TryGetValue(typeof(T).Name, out var wrapperTypeName))
            throw new InvalidOperationException(
                $"Entity '{typeof(T).Name}' is not mapped to any service wrapper.");
        var wrapperType = RemoteDataContext.ResolveWrapperType(wrapperTypeName);
        return (IDataContextServiceWrapper)_serviceProvider.GetRequiredService(wrapperType);
    }

    private void TrackEntity(T entity)
    {
        if (!RemoteDataContext.HasTracker<T>()) return;
        var tracker = RemoteDataContext.GetTracker<T>();
        var pk = tracker.GetPrimaryKey(entity);
        var snapshot = tracker.Snapshot(entity);
        _trackedEntities.Add(new TrackedEntity
        {
            EntityTypeName = typeof(T).Name,
            PrimaryKey = pk,
            Snapshot = snapshot
        });
    }
}
