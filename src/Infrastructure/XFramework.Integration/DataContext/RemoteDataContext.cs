using System.Reflection;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext;

public class RemoteDataContext(
    IServiceProvider serviceProvider,
    RequestMetadata? metadata = null) : IDataContext
{
    private static readonly Dictionary<string, Type> ResolvedWrapperTypes = new();
    private static Dictionary<string, string>? _wrapperMap;
    private static Type? _changeTrackerRegistryType;
    private static MethodInfo? _hasTrackerMethod;
    private static MethodInfo? _getTrackerMethod;

    internal readonly List<TrackedEntity> TrackedEntities = [];
    private readonly List<PendingChange> _pendingChanges = [];

    public IRemoteQuery<T> Query<T>() where T : class
    {
        return new RemoteQuery<T>(serviceProvider, TrackedEntities, metadata);
    }

    public void Add<T>(T entity) where T : class
    {
        _pendingChanges.Add(new PendingChange
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Add,
            SerializedEntity = MemoryPackSerializer.Serialize(entity)
        });
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
                var patch = tracker.Diff(entity, tracked.Snapshot);
                if (patch is null) return;

                _pendingChanges.Add(new PendingChange
                {
                    EntityTypeName = typeof(T).Name,
                    Operation = ChangeOperation.Update,
                    SerializedEntity = MemoryPackSerializer.Serialize(patch)
                });
                return;
            }
        }

        _pendingChanges.Add(new PendingChange
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Update,
            SerializedEntity = MemoryPackSerializer.Serialize(entity)
        });
    }

    public void Remove<T>(T entity) where T : class
    {
        _pendingChanges.Add(new PendingChange
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Remove,
            SerializedEntity = MemoryPackSerializer.Serialize(entity)
        });
    }

    public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_pendingChanges.Count == 0) return DataContextResult.Success();

        var wrapperMap = GetServiceWrapperMap();

        var wrapperTypeNames = _pendingChanges
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
            Changes = _pendingChanges.Select(c => new ChangeEntry
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
            _pendingChanges.Clear();

        return result ?? DataContextResult.Failure("Failed to deserialize response.");
    }

    internal static Type ResolveWrapperType(string metadataName)
    {
        if (ResolvedWrapperTypes.TryGetValue(metadataName, out var cached))
            return cached;

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(metadataName))
            .FirstOrDefault(t => t is not null)
            ?? throw new InvalidOperationException($"Could not resolve wrapper type '{metadataName}'");

        ResolvedWrapperTypes[metadataName] = type;
        return type;
    }

    internal static Dictionary<string, string> GetServiceWrapperMap()
    {
        if (_wrapperMap is not null) return _wrapperMap;

        var registrationType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("XFramework.Core.DataContext.DataContextEntityRegistrations"))
            .FirstOrDefault(t => t is not null);

        if (registrationType is null)
            throw new InvalidOperationException(
                "DataContextEntityRegistrations not found. Ensure the source generator has run and the assembly is loaded.");

        var method = registrationType.GetMethod("GetDataContextServiceWrapperMap",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetDataContextServiceWrapperMap method not found.");

        _wrapperMap = (Dictionary<string, string>)method.Invoke(null, null)!;
        return _wrapperMap;
    }

    internal static bool HasTracker<T>() where T : class
    {
        EnsureChangeTrackerRegistry();
        if (_changeTrackerRegistryType is null) return false;

        var method = _hasTrackerMethod!.MakeGenericMethod(typeof(T));
        return (bool)method.Invoke(null, null)!;
    }

    internal static IEntityChangeTracker<T> GetTracker<T>() where T : class
    {
        EnsureChangeTrackerRegistry();
        var method = _getTrackerMethod!.MakeGenericMethod(typeof(T));
        return (IEntityChangeTracker<T>)method.Invoke(null, null)!;
    }

    private static void EnsureChangeTrackerRegistry()
    {
        if (_changeTrackerRegistryType is not null || _hasTrackerMethod is not null) return;

        _changeTrackerRegistryType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("XFramework.Integration.DataContext.ChangeTrackerRegistry"))
            .FirstOrDefault(t => t is not null);

        if (_changeTrackerRegistryType is null) return;

        _hasTrackerMethod = _changeTrackerRegistryType.GetMethod("HasTracker",
            BindingFlags.Public | BindingFlags.Static);
        _getTrackerMethod = _changeTrackerRegistryType.GetMethod("GetTracker",
            BindingFlags.Public | BindingFlags.Static);
    }

    private record PendingChange
    {
        public required string EntityTypeName { get; init; }
        public required ChangeOperation Operation { get; init; }
        public required byte[] SerializedEntity { get; init; }
    }
}
