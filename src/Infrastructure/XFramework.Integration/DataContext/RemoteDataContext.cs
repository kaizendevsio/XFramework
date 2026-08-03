using System.Collections.Concurrent;
using System.Reflection;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext;

public class RemoteDataContext(
    IServiceProvider serviceProvider,
    RequestMetadata? metadata = null) : IDataContext
{
    private static readonly ConcurrentDictionary<string, Type> ResolvedWrapperTypes = new();
    private static readonly ConcurrentDictionary<Type, object> ResolvedChangeTrackers = new();
    private static readonly object ChangeTrackerLock = new();
    private static readonly object WrapperMapLock = new();
    private static Dictionary<string, string>? _wrapperMap;

    internal readonly List<TrackedEntity> TrackedEntities = [];
    private readonly List<PendingChange> _pendingChanges = [];
    private readonly object _pendingChangesLock = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public IRemoteQuery<T> Query<T>() where T : class
    {
        return new RemoteQuery<T>(serviceProvider, TrackedEntities, metadata);
    }

    public void Add<T>(T entity) where T : class
    {
        lock (_pendingChangesLock)
        {
            _pendingChanges.Add(new PendingChange
            {
                EntityTypeName = typeof(T).Name,
                EntityId = GetEntityId(entity),
                Operation = ChangeOperation.Add,
                SerializedEntity = MemoryPackSerializer.Serialize(entity),
                ApplyPersistedState = state => ApplyPersistedState(entity, state)
            });
        }
    }

    public void Update<T>(T entity) where T : class
    {
        if (HasTracker<T>())
        {
            var tracker = GetTracker<T>();
            var pk = tracker.GetPrimaryKey(entity);
            var tracked = TrackedEntities.FirstOrDefault(t =>
                t.EntityTypeName == typeof(T).Name && t.PrimaryKey == pk);

            if (tracked?.Snapshot is not null)
            {
                RejectLifecycleUpdate(entity, tracked);
                var patch = tracker.Diff(entity, tracked.Snapshot);
                if (patch is null) return;

                lock (_pendingChangesLock)
                {
                    _pendingChanges.Add(new PendingChange
                    {
                        EntityTypeName = typeof(T).Name,
                        EntityId = pk,
                        Operation = ChangeOperation.Update,
                        SerializedEntity = MemoryPackSerializer.Serialize(patch),
                        ApplyPersistedState = state => ApplyPersistedState(entity, state)
                    });
                }
                return;
            }
        }

        lock (_pendingChangesLock)
        {
            _pendingChanges.Add(new PendingChange
            {
                EntityTypeName = typeof(T).Name,
                EntityId = GetEntityId(entity),
                Operation = ChangeOperation.Update,
                SerializedEntity = MemoryPackSerializer.Serialize(entity),
                ApplyPersistedState = state => ApplyPersistedState(entity, state)
            });
        }
    }

    public void Remove<T>(T entity) where T : class
    {
        lock (_pendingChangesLock)
        {
            _pendingChanges.Add(new PendingChange
            {
                EntityTypeName = typeof(T).Name,
                EntityId = GetEntityId(entity),
                Operation = ChangeOperation.Remove,
                SerializedEntity = MemoryPackSerializer.Serialize(entity),
                ApplyPersistedState = _ => RemoveTrackedEntity(entity)
            });
        }
    }

    public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            List<PendingChange> batch;
            lock (_pendingChangesLock)
            {
                if (_pendingChanges.Count == 0)
                    return DataContextResult.Success();
                batch = [.. _pendingChanges];
            }

            var wrapperMap = GetServiceWrapperMap();

            var wrapperTypeNames = batch
                .Select(c =>
                {
                    if (!wrapperMap.TryGetValue(c.EntityTypeName, out var wrapperTypeName))
                        throw new InvalidOperationException(
                            $"Entity '{c.EntityTypeName}' is not mapped to any service.");
                    return wrapperTypeName;
                })
                .Distinct()
                .ToList();

            if (wrapperTypeNames.Count > 1)
                throw new InvalidOperationException(
                    "SaveChangesAsync spans multiple services. Split changes into separate IDataContext scopes per service.");

            var wrapperType = ResolveWrapperType(wrapperTypeNames[0]);
            var wrapper = (IDataContextServiceWrapper)serviceProvider.GetRequiredService(wrapperType);

            var request = new SaveChangesRequest
            {
                Changes = batch.Select(c => new ChangeEntry
                {
                    EntityTypeName = c.EntityTypeName,
                    Operation = c.Operation,
                    SerializedEntity = c.SerializedEntity
                }).ToList(),
                Metadata = metadata
            };

            var resultBytes = await wrapper.ExecuteChangesAsync(
                MemoryPackSerializer.Serialize(request), ct);
            var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

            if (result is { IsSuccess: true })
            {
                var persistedStates = result.PersistedEntities ?? [];
                foreach (var pending in batch)
                {
                    var state = persistedStates.FirstOrDefault(candidate =>
                        candidate.EntityTypeName.Equals(pending.EntityTypeName, StringComparison.OrdinalIgnoreCase) &&
                        candidate.EntityId == pending.EntityId);
                    if (state is not null)
                        pending.ApplyPersistedState(state);
                }

                lock (_pendingChangesLock)
                {
                    _pendingChanges.RemoveAll(pending =>
                        batch.Any(sent => ReferenceEquals(sent, pending)));
                }
            }

            return result ?? DataContextResult.Failure("Failed to deserialize response.");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    internal static Type ResolveWrapperType(string metadataName)
    {
        return ResolvedWrapperTypes.GetOrAdd(metadataName, static name =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name))
                .FirstOrDefault(type => type is not null)
            ?? throw new InvalidOperationException($"Could not resolve wrapper type '{name}'"));
    }

    internal static Dictionary<string, string> GetServiceWrapperMap()
    {
        if (_wrapperMap is not null) return _wrapperMap;

        lock (WrapperMapLock)
        {
            if (_wrapperMap is not null) return _wrapperMap;

            var registrationTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("XFramework.Core.DataContext.DataContextEntityRegistrations"))
                .Where(t => t is not null)
                .Cast<Type>()
                .ToList();

            if (registrationTypes.Count == 0)
                throw new InvalidOperationException(
                    "DataContextEntityRegistrations not found. Ensure the source generator has run and the assembly is loaded.");

            var mergedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var registrationType in registrationTypes)
            {
                var method = registrationType.GetMethod("GetDataContextServiceWrapperMap",
                    BindingFlags.Public | BindingFlags.Static);
                if (method is null) continue;

                if (method.Invoke(null, null) is not Dictionary<string, string> map) continue;

                foreach (var (entityName, wrapperTypeName) in map)
                {
                    if (mergedMap.TryGetValue(entityName, out var existing)
                        && !string.Equals(existing, wrapperTypeName, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Entity '{entityName}' is mapped to multiple service wrappers: '{existing}' and '{wrapperTypeName}'.");
                    }

                    mergedMap[entityName] = wrapperTypeName;
                }
            }

            if (mergedMap.Count == 0)
                throw new InvalidOperationException("GetDataContextServiceWrapperMap returned no entity mappings.");

            _wrapperMap = mergedMap;
            return _wrapperMap;
        }
    }

    internal static bool HasTracker<T>() where T : class
    {
        return TryResolveTracker<T>(out _);
    }

    internal static IEntityChangeTracker<T> GetTracker<T>() where T : class
    {
        return TryResolveTracker<T>(out var tracker)
            ? tracker!
            : throw new InvalidOperationException(
                $"No change tracker is registered for entity '{typeof(T).FullName}'.");
    }

    private static bool TryResolveTracker<T>(out IEntityChangeTracker<T>? tracker)
        where T : class
    {
        lock (ChangeTrackerLock)
        {
            if (ResolvedChangeTrackers.TryGetValue(typeof(T), out var cached))
            {
                tracker = (IEntityChangeTracker<T>)cached;
                return true;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var registryType = assembly.GetType(
                    "XFramework.Integration.DataContext.ChangeTrackerRegistry");
                var hasTracker = registryType?.GetMethod(
                    "HasTracker", BindingFlags.Public | BindingFlags.Static);
                var getTracker = registryType?.GetMethod(
                    "GetTracker", BindingFlags.Public | BindingFlags.Static);

                if (hasTracker is null || getTracker is null)
                    continue;

                var hasEntityTracker = (bool)hasTracker
                    .MakeGenericMethod(typeof(T))
                    .Invoke(null, null)!;
                if (!hasEntityTracker)
                    continue;

                tracker = (IEntityChangeTracker<T>)getTracker
                    .MakeGenericMethod(typeof(T))
                    .Invoke(null, null)!;
                ResolvedChangeTrackers[typeof(T)] = tracker;
                return true;
            }
        }

        tracker = null;
        return false;
    }

    private static void RejectLifecycleUpdate<T>(T entity, TrackedEntity tracked) where T : class
    {
        if (entity is not ISoftDeletable lifecycle)
            return;

        if (tracked.OriginalIsDeleted != lifecycle.IsDeleted)
            throw new InvalidOperationException(
                $"Entity '{typeof(T).Name}' deletion must use IDataContext.Remove or an approved lifecycle workflow.");

        if (tracked.OriginalIsEnabled != lifecycle.IsEnabled)
            throw new InvalidOperationException(
                $"Entity '{typeof(T).Name}' enabled state must use an approved lifecycle workflow.");
    }

    private void ApplyPersistedState<T>(T entity, PersistedEntityState state) where T : class
    {
        if (entity is IHasConcurrencyStamp stamped && state.ConcurrencyStamp is { } stamp)
            stamped.ConcurrencyStamp = stamp;

        UpsertTrackedEntity(entity);
    }

    private void UpsertTrackedEntity<T>(T entity) where T : class
    {
        if (!HasTracker<T>()) return;

        var tracker = GetTracker<T>();
        var pk = tracker.GetPrimaryKey(entity);
        TrackedEntities.RemoveAll(tracked =>
            tracked.EntityTypeName.Equals(typeof(T).Name, StringComparison.OrdinalIgnoreCase) &&
            tracked.PrimaryKey == pk);
        TrackedEntities.Add(new TrackedEntity
        {
            EntityTypeName = typeof(T).Name,
            PrimaryKey = pk,
            Snapshot = tracker.Snapshot(entity),
            OriginalIsEnabled = (entity as ISoftDeletable)?.IsEnabled,
            OriginalIsDeleted = (entity as ISoftDeletable)?.IsDeleted
        });
    }

    private void RemoveTrackedEntity<T>(T entity) where T : class
    {
        if (!HasTracker<T>()) return;

        var tracker = GetTracker<T>();
        var pk = tracker.GetPrimaryKey(entity);
        TrackedEntities.RemoveAll(tracked =>
            tracked.EntityTypeName.Equals(typeof(T).Name, StringComparison.OrdinalIgnoreCase) &&
            tracked.PrimaryKey == pk);
    }

    private static Guid GetEntityId<T>(T entity) where T : class =>
        entity is IHasId identified
            ? identified.Id
            : throw new InvalidOperationException(
                $"Remote DataContext entity '{typeof(T).Name}' must implement IHasId.");

    private record PendingChange
    {
        public required string EntityTypeName { get; init; }
        public required Guid EntityId { get; init; }
        public required ChangeOperation Operation { get; init; }
        public required byte[] SerializedEntity { get; init; }
        public required Action<PersistedEntityState> ApplyPersistedState { get; init; }
    }
}
