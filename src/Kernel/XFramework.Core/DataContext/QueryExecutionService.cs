using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public sealed class QueryExecutionService(
    IServiceProvider serviceProvider,
    ILogger<QueryExecutionService> logger)
    : IQueryExecutionService
{
    private readonly ConcurrentDictionary<string, Type> _entityTypes = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterEntity<T>(string name) where T : class
        => RegisterEntity(typeof(T), name);

    public void RegisterEntity(Type entityType, string name)
    {
        _entityTypes[name] = entityType;
        logger.LogDebug("Registered entity type '{Name}' → {Type}", name, entityType.FullName);
    }

    public async Task<byte[]> ExecuteAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
    {
        var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes);
        if (descriptor is null)
            return SerializeError("Failed to deserialize QueryDescriptor.");

        if (!_entityTypes.TryGetValue(descriptor.EntityTypeName, out var entityType))
            return SerializeError($"Entity type '{descriptor.EntityTypeName}' is not registered. Query rejected.");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

            var result = await QueryDescriptorExecutor.ExecuteAsync(dbContext, entityType, descriptor, ct);
            return MemoryPack.MemoryPackSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute query for entity '{EntityType}'", descriptor.EntityTypeName);
            return SerializeError(ex.Message);
        }
    }

    public async IAsyncEnumerable<byte[]> ExecuteStreamAsync(
        byte[] queryDescriptorBytes,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes);
        if (descriptor is null)
            yield break;

        if (!_entityTypes.TryGetValue(descriptor.EntityTypeName, out var entityType))
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

            foreach (var change in request.Changes)
            {
                if (!_entityTypes.TryGetValue(change.EntityTypeName, out var entityType))
                    return SerializeError($"Entity type '{change.EntityTypeName}' is not registered.");

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

                        foreach (var (propertyName, valueBytes) in patch.Changes)
                        {
                            var prop = entityType.GetProperty(propertyName);
                            if (prop is null) continue;

                            var value = MemoryPack.MemoryPackSerializer.Deserialize(
                                prop.PropertyType, (ReadOnlySpan<byte>)valueBytes);
                            prop.SetValue(existing, value);
                        }

                        continue;
                    }
                }

                // Deserialize as full entity for Add, Remove, or Update fallback
                var entity = MemoryPack.MemoryPackSerializer.Deserialize(entityType, (ReadOnlySpan<byte>)change.SerializedEntity);
                if (entity is null)
                    return SerializeError($"Failed to deserialize entity of type '{change.EntityTypeName}'.");

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
            }

            await dbContext.SaveChangesAsync(ct);

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

    private static byte[] SerializeError(string message)
    {
        var result = DataContextResult.Failure(message);
        return MemoryPack.MemoryPackSerializer.Serialize(result);
    }
}
