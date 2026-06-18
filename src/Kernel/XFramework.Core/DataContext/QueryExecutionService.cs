using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public sealed class QueryExecutionService(
    IServiceProvider serviceProvider,
    ILogger<QueryExecutionService> logger)
    : IQueryExecutionService
{
    private readonly ConcurrentDictionary<string, Type> _entityTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _mutableEntityTypes = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterEntity<T>(string name) where T : class
        => RegisterEntity(typeof(T), name);

    public void RegisterEntity(Type entityType, string name)
        => RegisterEntity(entityType, name, allowRemoteMutation: true);

    public void RegisterEntity(Type entityType, string name, bool allowRemoteMutation)
    {
        _entityTypes[name] = entityType;
        _mutableEntityTypes[name] = allowRemoteMutation;
        logger.LogDebug("Registered entity type '{Name}' → {Type}", name, entityType.FullName);
    }

    public async Task<byte[]> ExecuteAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
    {
        var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes);
        if (descriptor is null)
            return SerializeError("Failed to deserialize QueryDescriptor.");

        if (!_entityTypes.TryGetValue(descriptor.EntityTypeName, out var entityType))
            return SerializeError($"Entity type '{descriptor.EntityTypeName}' is not registered. Query rejected.");

        if (!HasRequiredQueryTenantMetadata(entityType, descriptor.Metadata))
            return SerializeError($"Entity type '{descriptor.EntityTypeName}' requires tenant metadata for remote DataContext query.");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

            var result = await QueryDescriptorExecutor.ExecuteAsync(dbContext, entityType, descriptor, ct);

            // MemoryPackSerializer.Serialize(object?) uses the object formatter which fails
            // for runtime-typed results. Use the actual result type for correct serialization.
            var resultType = GetResultType(descriptor.Mode, entityType);
            return MemoryPack.MemoryPackSerializer.Serialize(resultType, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute query for entity '{EntityType}'", descriptor.EntityTypeName);
            return SerializeError(ex.Message);
        }
    }

    private static Type GetResultType(QueryExecutionMode mode, Type entityType) => mode switch
    {
        QueryExecutionMode.ToList => typeof(List<>).MakeGenericType(entityType),
        QueryExecutionMode.FirstOrDefault or QueryExecutionMode.SingleOrDefault
            or QueryExecutionMode.MinBy or QueryExecutionMode.MaxBy => entityType,
        QueryExecutionMode.Count => typeof(int),
        QueryExecutionMode.Any or QueryExecutionMode.AnyWithPredicate or QueryExecutionMode.All => typeof(bool),
        QueryExecutionMode.Sum => typeof(decimal),
        QueryExecutionMode.Average => typeof(double),
        _ => typeof(object)
    };

    public async IAsyncEnumerable<byte[]> ExecuteStreamAsync(
        byte[] queryDescriptorBytes,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes);
        if (descriptor is null)
            yield break;

        if (!_entityTypes.TryGetValue(descriptor.EntityTypeName, out var entityType))
            yield break;

        if (!HasRequiredQueryTenantMetadata(entityType, descriptor.Metadata))
            yield break;

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        await foreach (var item in QueryDescriptorExecutor.ExecuteStreamAsync(dbContext, entityType, descriptor, ct))
        {
            yield return MemoryPack.MemoryPackSerializer.Serialize(item);
        }
    }

    public async Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default)
    {
        var request = MemoryPack.MemoryPackSerializer.Deserialize<SaveChangesRequest>((ReadOnlySpan<byte>)saveChangesRequestBytes);
        if (request is null)
            return SerializeError("Failed to deserialize SaveChangesRequest.");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            var changedTenantModuleFeatures = new List<(Guid TenantId, string ModuleKey, string SubFeatureKey)>();

            foreach (var change in request.Changes)
            {
                if (!_entityTypes.TryGetValue(change.EntityTypeName, out var entityType))
                    return SerializeError($"Entity type '{change.EntityTypeName}' is not registered.");

                if (!_mutableEntityTypes.TryGetValue(change.EntityTypeName, out var canMutate) || !canMutate)
                    return SerializeError($"Entity type '{change.EntityTypeName}' is not registered for remote mutation.");

                // For Update operations, try FieldPatch first before deserializing as entity
                if (change.Operation == ChangeOperation.Update)
                {
                    FieldPatch? patch = null;
                    try
                    {
                        patch = MemoryPack.MemoryPackSerializer.Deserialize<FieldPatch>(
                            (ReadOnlySpan<byte>)change.SerializedEntity);
                    }
                    catch { }

                    if (patch is { EntityId.Length: > 0, Changes.Count: > 0 })
                    {
                        var pkValue = MemoryPack.MemoryPackSerializer.Deserialize<Guid>(
                            (ReadOnlySpan<byte>)patch.EntityId);
                        var existing = await dbContext.FindAsync(entityType, pkValue);
                        if (existing is null)
                            return SerializeError($"Entity '{change.EntityTypeName}' with PK '{pkValue}' not found.");

                        if (!ValidateTenantMetadata(existing, request.Metadata, entityType, out var patchMetadataError))
                            return SerializeError(patchMetadataError);

                        foreach (var (propertyName, valueBytes) in patch.Changes)
                        {
                            var prop = entityType.GetProperty(propertyName);
                            if (prop is null) continue;

                            if (string.Equals(prop.Name, nameof(IHasTenantId.TenantId), StringComparison.Ordinal))
                                return SerializeError($"Entity '{change.EntityTypeName}' TenantId cannot be changed through remote DataContext.");

                            var value = MemoryPack.MemoryPackSerializer.Deserialize(
                                prop.PropertyType, (ReadOnlySpan<byte>)valueBytes);
                            prop.SetValue(existing, value);
                        }

                        if (existing is TenantModuleFeature patchedFeature)
                        {
                            changedTenantModuleFeatures.Add((
                                patchedFeature.TenantId,
                                patchedFeature.ModuleKey,
                                patchedFeature.SubFeatureKey));
                        }

                        continue;
                    }
                }

                // Deserialize as full entity for Add, Remove, or Update fallback
                var entity = MemoryPack.MemoryPackSerializer.Deserialize(entityType, (ReadOnlySpan<byte>)change.SerializedEntity);
                if (entity is null)
                    return SerializeError($"Failed to deserialize entity of type '{change.EntityTypeName}'.");

                if (!ValidateTenantMetadata(entity, request.Metadata, entityType, out var metadataError))
                    return SerializeError(metadataError);

                switch (change.Operation)
                {
                    case ChangeOperation.Add:
                        dbContext.Add(entity);
                        break;
                    case ChangeOperation.Update:
                        dbContext.Update(entity);
                        break;
                    case ChangeOperation.Remove:
                        dbContext.Remove(entity);
                        break;
                }

                if (entity is TenantModuleFeature feature)
                {
                    changedTenantModuleFeatures.Add((
                        feature.TenantId,
                        feature.ModuleKey,
                        feature.SubFeatureKey));
                }
            }

            await dbContext.SaveChangesAsync(ct);
            InvalidateTenantModuleFeatureCache(scope.ServiceProvider, changedTenantModuleFeatures);

            var result = DataContextResult.Success();
            return MemoryPack.MemoryPackSerializer.Serialize(result);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to execute changes");
            var result = DataContextResult.Failure(ex.InnerException?.Message ?? ex.Message);
            return MemoryPack.MemoryPackSerializer.Serialize(result);
        }
    }

    private static bool ValidateTenantMetadata(
        object entity,
        RequestMetadata? metadata,
        Type entityType,
        out string error)
    {
        error = string.Empty;

        if (entity is not IHasTenantId tenantEntity || IsControlPlaneTenantRecord(entityType))
            return true;

        if (metadata?.TenantId is not { } metadataTenantId || metadataTenantId == Guid.Empty)
        {
            error = $"Entity '{entityType.Name}' requires tenant metadata for remote DataContext mutation.";
            return false;
        }

        if (tenantEntity.TenantId == Guid.Empty)
        {
            tenantEntity.TenantId = metadataTenantId;
            return true;
        }

        if (tenantEntity.TenantId == metadataTenantId)
            return true;

        error = $"Entity '{entityType.Name}' TenantId does not match request tenant metadata.";
        return false;
    }

    private static bool IsControlPlaneTenantRecord(Type entityType) =>
        entityType == typeof(Tenant) ||
        entityType == typeof(TenantModuleFeature);

    private static bool HasRequiredQueryTenantMetadata(Type entityType, RequestMetadata? metadata) =>
        !typeof(IHasTenantId).IsAssignableFrom(entityType) ||
        IsControlPlaneTenantRecord(entityType) ||
        metadata?.TenantId is { } tenantId && tenantId != Guid.Empty;

    private static void InvalidateTenantModuleFeatureCache(
        IServiceProvider scopedServices,
        IEnumerable<(Guid TenantId, string ModuleKey, string SubFeatureKey)> changedFeatures)
    {
        var featureService = scopedServices.GetService<ITenantModuleFeatureService>();
        if (featureService is null)
            return;

        foreach (var feature in changedFeatures.Distinct())
        {
            featureService.Invalidate(feature.TenantId, feature.ModuleKey, feature.SubFeatureKey);
        }
    }

    private static byte[] SerializeError(string message)
    {
        var result = DataContextResult.Failure(message);
        return MemoryPack.MemoryPackSerializer.Serialize(result);
    }
}
