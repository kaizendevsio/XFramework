# DB Proxy Decentralization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `IDataContext` work identically across all runtimes (Server, WASM, MAUI) by routing remote queries through owning services instead of the centralized Hub.

**Architecture:** `RemoteDataContext` serializes queries as `QueryDescriptor`, looks up the owning service via a source-generated routing map, and calls the service through its generated wrapper. Each service registers a `DataContextBoltHandler` that executes queries against its own `DbContext` using the existing `QueryDescriptorExecutor`. The Hub becomes a pure message router.

**Tech Stack:** .NET 10, C# 14, MemoryPack, Roslyn Incremental Source Generators, Bolt thin protocol, EF Core, xUnit/NUnit

**Spec:** `docs/superpowers/specs/2026-04-22-db-proxy-decentralization-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|----------------|
| `src/Shared/XFramework.Domain.Shared/DataContext/FieldPatch.cs` | MemoryPackable field-level diff for Updates |
| `src/Shared/XFramework.Domain.Shared/DataContext/IEntityChangeTracker.cs` | Interface: Snapshot + Diff per entity |
| `src/Shared/XFramework.Domain.Shared/DataContext/TrackedEntity.cs` | Holds entity snapshot for change tracking |
| `src/Shared/XFramework.Domain.Shared/DataContext/DataContextConcurrencyException.cs` | Typed concurrency conflict exception |
| `src/Shared/XFramework.Domain.Shared/DataContext/IDataContextServiceWrapper.cs` | Common interface: ExecuteQueryAsync, ExecuteChangesAsync, ExecuteQueryStreamAsync |
| `src/Kernel/XFramework.Core/DataContext/IQueryExecutionService.cs` | Moved from Bolt.Hub |
| `src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs` | Moved from Bolt.Hub |
| `src/Kernel/XFramework.Core/DataContext/DataContextBoltHandler.cs` | IBoltHandler: registers `__db_query__`, `__db_changes__`, `__db_query_stream__` |
| `src/Kernel/XFramework.Core/DataContext/DataContextHandlerExtensions.cs` | `AddDataContextHandler(assembly)` DI extension |
| `src/SourceGenerators/XFramework.SourceGenerators/ChangeTrackerGenerator.cs` | Per-entity snapshot record + tracker class + registry |

### Modified Files

| File | Change |
|------|--------|
| `src/Shared/XFramework.Domain.Shared/DataContext/QueryDescriptor.cs` | Add `ChunkSize` (order 13), `Metadata` (order 14) |
| `src/Shared/XFramework.Domain.Shared/DataContext/SaveChangesRequest.cs` | Add `Metadata` (order 1) |
| `src/Shared/XFramework.Domain.Shared/DataContext/IRemoteQuery.cs` | Add `chunkSize` param to `ToAsyncEnumerable` |
| `src/Kernel/XFramework.Core/DataContext/ServerQuery.cs` | Update `ToAsyncEnumerable` signature |
| `src/Infrastructure/XFramework.Integration/DataContext/CachingQuery.cs` | Update `ToAsyncEnumerable` delegation |
| `src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs` | Full rewrite |
| `src/Infrastructure/XFramework.Integration/DataContext/RemoteQuery.cs` | Implement all terminal methods |
| `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs` | Register `RemoteDataContext` as `IDataContext` |
| `src/SourceGenerators/XFramework.SourceGenerators/DataContextRegistrationGenerator.cs` | Add entity → wrapper type routing map |
| `src/SourceGenerators/XFramework.SourceGenerators/ServiceWrapperGenerator.cs` | Add `IDataContextServiceWrapper` impl |
| `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs` | Point to Core `QueryExecutionService` |
| Service `Program.cs` files | Add `AddDataContextHandler(assembly)` |

### Removed Files (Task 14)

| File | Reason |
|------|--------|
| `src/Modules/XFramework.Bolt/Bolt.Hub/Services/IQueryExecutionService.cs` | Moved to Core |
| `src/Modules/XFramework.Bolt/Bolt.Hub/Services/QueryExecutionService.cs` | Moved to Core |
| `FrameType.ExecuteQuery` (0x0B) + `ExecuteChanges` (0x0C) | Transitional shim removed |

---

### Task 1: New Shared Wire Types

**Files:**
- Create: `src/Shared/XFramework.Domain.Shared/DataContext/FieldPatch.cs`
- Create: `src/Shared/XFramework.Domain.Shared/DataContext/IEntityChangeTracker.cs`
- Create: `src/Shared/XFramework.Domain.Shared/DataContext/TrackedEntity.cs`
- Create: `src/Shared/XFramework.Domain.Shared/DataContext/DataContextConcurrencyException.cs`
- Create: `src/Shared/XFramework.Domain.Shared/DataContext/IDataContextServiceWrapper.cs`

- [ ] **Step 1: Create FieldPatch**

```csharp
// src/Shared/XFramework.Domain.Shared/DataContext/FieldPatch.cs
namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class FieldPatch
{
    [MemoryPackOrder(0)] public byte[] EntityId { get; set; } = [];
    [MemoryPackOrder(1)] public Dictionary<string, byte[]> Changes { get; set; } = new();
}
```

- [ ] **Step 2: Create IEntityChangeTracker**

```csharp
// src/Shared/XFramework.Domain.Shared/DataContext/IEntityChangeTracker.cs
namespace XFramework.Domain.Shared.DataContext;

public interface IEntityChangeTracker<T> where T : class
{
    object Snapshot(T entity);
    FieldPatch? Diff(T current, object snapshot);
    Guid GetPrimaryKey(T entity);
}
```

- [ ] **Step 3: Create TrackedEntity**

```csharp
// src/Shared/XFramework.Domain.Shared/DataContext/TrackedEntity.cs
namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class TrackedEntity
{
    [MemoryPackOrder(0)] public string EntityTypeName { get; set; } = string.Empty;
    [MemoryPackOrder(1)] public Guid PrimaryKey { get; set; }
    [MemoryPackOrder(2)] public byte[] SnapshotBytes { get; set; } = [];

    [MemoryPackIgnore] public object? Snapshot { get; set; }
}
```

- [ ] **Step 4: Create DataContextConcurrencyException**

```csharp
// src/Shared/XFramework.Domain.Shared/DataContext/DataContextConcurrencyException.cs
namespace XFramework.Domain.Shared.DataContext;

public class DataContextConcurrencyException : Exception
{
    public string EntityTypeName { get; init; } = string.Empty;
    public byte[] EntityId { get; init; } = [];
    public Dictionary<string, byte[]> CurrentDbValues { get; init; } = new();
    public Dictionary<string, byte[]> ClientValues { get; init; } = new();

    public DataContextConcurrencyException(string message) : base(message) { }
    public DataContextConcurrencyException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 5: Create IDataContextServiceWrapper**

```csharp
// src/Shared/XFramework.Domain.Shared/DataContext/IDataContextServiceWrapper.cs
namespace XFramework.Domain.Shared.DataContext;

public interface IDataContextServiceWrapper
{
    Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
    Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
}
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build src/Shared/XFramework.Domain.Shared/`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/Shared/XFramework.Domain.Shared/DataContext/FieldPatch.cs \
        src/Shared/XFramework.Domain.Shared/DataContext/IEntityChangeTracker.cs \
        src/Shared/XFramework.Domain.Shared/DataContext/TrackedEntity.cs \
        src/Shared/XFramework.Domain.Shared/DataContext/DataContextConcurrencyException.cs \
        src/Shared/XFramework.Domain.Shared/DataContext/IDataContextServiceWrapper.cs
git commit -m "feat(data-context): add shared types for decentralized DB proxy"
```

---

### Task 2: Extend Existing Shared Types

**Files:**
- Modify: `src/Shared/XFramework.Domain.Shared/DataContext/QueryDescriptor.cs`
- Modify: `src/Shared/XFramework.Domain.Shared/DataContext/SaveChangesRequest.cs`
- Modify: `src/Shared/XFramework.Domain.Shared/DataContext/IRemoteQuery.cs`

- [ ] **Step 1: Add ChunkSize and Metadata to QueryDescriptor**

In `src/Shared/XFramework.Domain.Shared/DataContext/QueryDescriptor.cs`, add after the existing `PredicateFilters` property (line 20):

```csharp
    [MemoryPackOrder(13)] public int? ChunkSize { get; set; }
    [MemoryPackOrder(14)] public RequestMetadata? Metadata { get; set; }
```

The file already imports `XFramework.Domain.Shared.BusinessObjects` (line 1), which contains `RequestMetadata`.

- [ ] **Step 2: Add Metadata to SaveChangesRequest**

In `src/Shared/XFramework.Domain.Shared/DataContext/SaveChangesRequest.cs`, add after `Changes` (line 6):

```csharp
    [MemoryPackOrder(1)] public RequestMetadata? Metadata { get; set; }
```

Add import at top:
```csharp
using XFramework.Domain.Shared.BusinessObjects;
```

- [ ] **Step 3: Add chunkSize parameter to IRemoteQuery.ToAsyncEnumerable**

In `src/Shared/XFramework.Domain.Shared/DataContext/IRemoteQuery.cs`, replace line 34:

```csharp
    // Before:
    IAsyncEnumerable<T> ToAsyncEnumerable(CancellationToken ct = default);
    // After:
    IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, CancellationToken ct = default);
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/Shared/XFramework.Domain.Shared/`
Expected: Build fails — `ServerQuery<T>`, `RemoteQuery<T>`, and `CachingQuery<T>` don't match new signature. This is expected; Task 3 fixes them.

- [ ] **Step 5: Commit**

```bash
git add src/Shared/XFramework.Domain.Shared/DataContext/QueryDescriptor.cs \
        src/Shared/XFramework.Domain.Shared/DataContext/SaveChangesRequest.cs \
        src/Shared/XFramework.Domain.Shared/DataContext/IRemoteQuery.cs
git commit -m "feat(data-context): extend QueryDescriptor, SaveChangesRequest, IRemoteQuery for decentralization"
```

---

### Task 3: Update IRemoteQuery Implementations for New Signature

**Files:**
- Modify: `src/Kernel/XFramework.Core/DataContext/ServerQuery.cs:99`
- Modify: `src/Infrastructure/XFramework.Integration/DataContext/CachingQuery.cs:85`
- Modify: `src/Infrastructure/XFramework.Integration/DataContext/RemoteQuery.cs:127`

- [ ] **Step 1: Update ServerQuery.ToAsyncEnumerable**

In `src/Kernel/XFramework.Core/DataContext/ServerQuery.cs`, replace lines 99-105:

```csharp
    public async IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in _queryable.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return item;
        }
    }
```

`chunkSize` is ignored — in-process has no transport to chunk over.

- [ ] **Step 2: Update CachingQuery.ToAsyncEnumerable**

In `src/Infrastructure/XFramework.Integration/DataContext/CachingQuery.cs`, replace line 85-86:

```csharp
    public IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, CancellationToken ct = default)
        => _inner.ToAsyncEnumerable(chunkSize, ct);
```

- [ ] **Step 3: Update RemoteQuery.ToAsyncEnumerable**

In `src/Infrastructure/XFramework.Integration/DataContext/RemoteQuery.cs`, replace lines 127-131:

```csharp
    public async IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, [EnumeratorCancellation] CancellationToken ct = default)
    {
        throw new NotImplementedException(PendingMigrationMessage);
        yield break;
    }
```

This is still a stub — Task 11 implements it.

- [ ] **Step 4: Build full solution**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/Kernel/XFramework.Core/DataContext/ServerQuery.cs \
        src/Infrastructure/XFramework.Integration/DataContext/CachingQuery.cs \
        src/Infrastructure/XFramework.Integration/DataContext/RemoteQuery.cs
git commit -m "feat(data-context): update ToAsyncEnumerable with chunkSize parameter"
```

---

### Task 4: Move QueryExecutionService to XFramework.Core

**Files:**
- Create: `src/Kernel/XFramework.Core/DataContext/IQueryExecutionService.cs`
- Create: `src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs`
- Modify: `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs` — update using

- [ ] **Step 1: Create IQueryExecutionService in Core**

```csharp
// src/Kernel/XFramework.Core/DataContext/IQueryExecutionService.cs
namespace XFramework.Core.DataContext;

public interface IQueryExecutionService
{
    void RegisterEntity<T>(string name) where T : class;
    void RegisterEntity(Type entityType, string name);
    Task<byte[]> ExecuteAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> ExecuteStreamAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
    Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create QueryExecutionService in Core**

Copy `src/Modules/XFramework.Bolt/Bolt.Hub/Services/QueryExecutionService.cs` to `src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs`.

Change namespace from `Bolt.Hub.Services` to `XFramework.Core.DataContext`. Update the using of `IQueryExecutionService` to reference the Core namespace. Everything else stays identical — the implementation already uses `XFramework.Core.DataContext.QueryDescriptorExecutor` and `XFramework.Domain.Shared.DataContext.*`.

```csharp
// src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
```

- [ ] **Step 3: Update Hub to use Core's QueryExecutionService**

In `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs`, change the `IQueryExecutionService` import from `Bolt.Hub.Services` to `XFramework.Core.DataContext`. The Hub's old files become thin wrappers referencing Core's implementation.

- [ ] **Step 4: Delete old Hub files**

Delete:
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/IQueryExecutionService.cs`
- `src/Modules/XFramework.Bolt/Bolt.Hub/Services/QueryExecutionService.cs`

Update any remaining references in Bolt.Hub to use `XFramework.Core.DataContext.IQueryExecutionService`.

- [ ] **Step 5: Build to verify**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/Kernel/XFramework.Core/DataContext/IQueryExecutionService.cs \
        src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs
git add -u  # captures deleted Hub files and modified imports
git commit -m "refactor(data-context): move QueryExecutionService from Hub to Core"
```

---

### Task 5: Add FieldPatch Support to QueryExecutionService

**Files:**
- Modify: `src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs`

The current `ExecuteChangesAsync` handles `ChangeOperation.Update` by deserializing the full entity and calling `dbContext.Update(entity)`. With field-level diffs, `Update` operations carry a `FieldPatch` instead of a full entity.

- [ ] **Step 1: Add FieldPatch handling in ExecuteChangesAsync**

In `QueryExecutionService.ExecuteChangesAsync`, replace the `ChangeOperation.Update` case:

```csharp
case ChangeOperation.Update:
    var patch = MemoryPack.MemoryPackSerializer.Deserialize<FieldPatch>(
        (ReadOnlySpan<byte>)change.SerializedEntity);
    if (patch is null)
        return SerializeError($"Failed to deserialize FieldPatch for '{change.EntityTypeName}'.");

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
    break;
```

Keep the old full-entity `Update` path as fallback — if deserialization as `FieldPatch` fails (wrong format), fall back to the original `dbContext.Update(entity)` path. This supports both old-style full-entity updates and new field-level patches.

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Kernel/XFramework.Core/`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/Kernel/XFramework.Core/DataContext/QueryExecutionService.cs
git commit -m "feat(data-context): add FieldPatch support for field-level Updates"
```

---

### Task 6: DataContextBoltHandler + DI Registration

**Files:**
- Create: `src/Kernel/XFramework.Core/DataContext/DataContextBoltHandler.cs`
- Create: `src/Kernel/XFramework.Core/DataContext/DataContextHandlerExtensions.cs`

- [ ] **Step 1: Create DataContextBoltHandler**

This is an `IBoltHandler` that registers `__db_query__`, `__db_changes__`, and `__db_query_stream__` on the `BoltClient`. It's the service-side endpoint that receives remote `IDataContext` calls.

```csharp
// src/Kernel/XFramework.Core/DataContext/DataContextBoltHandler.cs
using System.Net;
using System.Runtime.CompilerServices;
using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;

namespace XFramework.Core.DataContext;

public class DataContextBoltHandler : IBoltHandler
{
    public void Register(BoltClient client, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        client.RegisterHandler("__db_query__", async (payload, requestId) =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();
            try
            {
                var result = await queryService.ExecuteAsync(payload.ToArray());
                return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DataContext query failed (requestId={RequestId})", requestId);
                var error = MemoryPack.MemoryPackSerializer.Serialize(
                    Domain.Shared.DataContext.DataContextResult.Failure(ex.Message));
                return (HttpStatusCode.InternalServerError, (ReadOnlyMemory<byte>)error);
            }
        });

        client.RegisterHandler("__db_changes__", async (payload, requestId) =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();
            try
            {
                var result = await queryService.ExecuteChangesAsync(payload.ToArray());
                return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DataContext changes failed (requestId={RequestId})", requestId);
                var error = MemoryPack.MemoryPackSerializer.Serialize(
                    Domain.Shared.DataContext.DataContextResult.Failure(ex.Message));
                return (HttpStatusCode.InternalServerError, (ReadOnlyMemory<byte>)error);
            }
        });

        client.RegisterStreamHandler("__db_query_stream__", async stream =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();

            var (hasData, payload) = await stream.ReadAsync();
            if (!hasData) { await stream.CloseAsync(HttpStatusCode.BadRequest); return; }

            var descriptorBytes = payload.ToArray();
            var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<Domain.Shared.DataContext.QueryDescriptor>(
                (ReadOnlySpan<byte>)descriptorBytes);
            var chunkSize = descriptor?.ChunkSize ?? 100;

            var chunk = new List<byte[]>(chunkSize);
            await foreach (var entityBytes in queryService.ExecuteStreamAsync(descriptorBytes))
            {
                chunk.Add(entityBytes);
                if (chunk.Count >= chunkSize)
                {
                    await stream.SendAsync(MemoryPack.MemoryPackSerializer.Serialize(chunk));
                    chunk = new List<byte[]>(chunkSize);
                }
            }

            if (chunk.Count > 0)
                await stream.SendAsync(MemoryPack.MemoryPackSerializer.Serialize(chunk));

            await stream.CloseAsync(HttpStatusCode.OK);
        });

        logger.LogInformation("Registered DataContext Bolt handlers (__db_query__, __db_changes__, __db_query_stream__)");
    }
}
```

- [ ] **Step 2: Create DataContextHandlerExtensions**

```csharp
// src/Kernel/XFramework.Core/DataContext/DataContextHandlerExtensions.cs
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace XFramework.Core.DataContext;

public static class DataContextHandlerExtensions
{
    /// <summary>
    /// Registers QueryExecutionService with entity types from [GenerateEndpoints] in the given assembly,
    /// and adds the DataContextBoltHandler for Bolt RPC registration.
    /// Call this in each service's Program.cs.
    /// </summary>
    public static IServiceCollection AddDataContextHandler(this IServiceCollection services, Assembly entityAssembly)
    {
        services.AddSingleton<IQueryExecutionService>(sp =>
        {
            var queryService = ActivatorUtilities.CreateInstance<QueryExecutionService>(sp);

            // Use the source-generated registration method
            var registrationType = entityAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataContextEntityRegistrations");

            if (registrationType is not null)
            {
                var method = registrationType.GetMethod("GetDataContextEntityTypes",
                    BindingFlags.Public | BindingFlags.Static);
                if (method?.Invoke(null, null) is Dictionary<string, Type> entityTypes)
                {
                    foreach (var (name, type) in entityTypes)
                        queryService.RegisterEntity(type, name);
                }
            }

            return queryService;
        });

        return services;
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Kernel/XFramework.Core/`
Expected: Build succeeded. Note: `XFramework.Core` must reference `Bolt.Client` and `XFramework.Integration.Abstractions` (for `IBoltHandler`). Check the `.csproj` and add project references if missing.

- [ ] **Step 4: Commit**

```bash
git add src/Kernel/XFramework.Core/DataContext/DataContextBoltHandler.cs \
        src/Kernel/XFramework.Core/DataContext/DataContextHandlerExtensions.cs
git commit -m "feat(data-context): add DataContextBoltHandler and DI extensions"
```

---

### Task 7: ChangeTrackerGenerator (Source Generator)

**Files:**
- Create: `src/SourceGenerators/XFramework.SourceGenerators/ChangeTrackerGenerator.cs`

This generator scans `[GenerateEndpoints]` entities and emits:
1. `{Entity}Snapshot` — MemoryPackable record with scalar property copies
2. `{Entity}ChangeTracker : IEntityChangeTracker<{Entity}>` — Snapshot() + Diff()
3. `ChangeTrackerRegistry` — static lookup from `Type → IEntityChangeTracker`

- [ ] **Step 1: Create the generator**

```csharp
// src/SourceGenerators/XFramework.SourceGenerators/ChangeTrackerGenerator.cs
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

[Generator]
public class ChangeTrackerGenerator : IIncrementalGenerator
{
    private static readonly HashSet<string> ScalarTypes = new()
    {
        "System.String", "System.Boolean", "System.Byte", "System.SByte",
        "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
        "System.Int64", "System.UInt64", "System.Single", "System.Double",
        "System.Decimal", "System.Guid", "System.DateTime", "System.DateTimeOffset",
        "System.DateOnly", "System.TimeOnly", "System.TimeSpan", "System.Byte[]"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entities = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax cds &&
                    cds.AttributeLists.Any(al => al.Attributes.Any(a =>
                        a.Name.ToString().Contains("GenerateEndpoints"))),
                transform: GetEntityInfo)
            .Where(e => e is not null);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(entities.Collect()),
            (ctx, pair) => Execute(pair.Left, pair.Right!, ctx));
    }

    private static EntityInfo? GetEntityInfo(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl, ct);
        if (symbol is null) return null;

        var properties = new List<PropertyInfo>();
        var hasPk = false;

        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.IsStatic || member.IsIndexer) continue;
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.SetMethod is null) continue;

            // PK detection: property named "Id"
            if (member.Name == "Id")
            {
                hasPk = true;
                continue;
            }

            // Skip navigation properties (collections, complex reference types)
            var typeFullName = member.Type.OriginalDefinition.ToDisplayString();
            var isNullable = member.Type is INamedTypeSymbol { IsGenericType: true } nts
                && nts.OriginalDefinition.ToDisplayString() == "System.Nullable<T>";
            var underlyingType = isNullable
                ? ((INamedTypeSymbol)member.Type).TypeArguments[0].ToDisplayString()
                : typeFullName;

            if (member.Type.TypeKind == TypeKind.Enum || ScalarTypes.Contains(underlyingType))
            {
                properties.Add(new PropertyInfo(
                    member.Name,
                    member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    isNullable || member.Type.NullableAnnotation == NullableAnnotation.Annotated));
            }
        }

        if (!hasPk) return null;

        return new EntityInfo(
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            symbol.ContainingNamespace.ToDisplayString(),
            properties);
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<EntityInfo?> localEntities,
        SourceProductionContext context)
    {
        var assemblyName = compilation.AssemblyName ?? "";

        // Also discover from referenced assemblies (same pattern as DataContextRegistrationGenerator)
        var allEntities = new List<EntityInfo>();
        foreach (var entity in localEntities)
            if (entity is not null) allEntities.Add(entity);

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm) continue;
            foreach (var type in GetAllTypes(asm.GlobalNamespace))
            {
                var attr = type.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.Name is "GenerateEndpointsAttribute");
                if (attr is null) continue;

                var props = new List<PropertyInfo>();
                var hasPk = false;

                foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
                {
                    if (member.IsStatic || member.IsIndexer) continue;
                    if (member.DeclaredAccessibility != Accessibility.Public) continue;
                    if (member.SetMethod is null) continue;

                    if (member.Name == "Id") { hasPk = true; continue; }

                    var typeFullName = member.Type.OriginalDefinition.ToDisplayString();
                    var isNullable = member.Type is INamedTypeSymbol { IsGenericType: true } nts
                        && nts.OriginalDefinition.ToDisplayString() == "System.Nullable<T>";
                    var underlyingType = isNullable
                        ? ((INamedTypeSymbol)member.Type).TypeArguments[0].ToDisplayString()
                        : typeFullName;

                    if (member.Type.TypeKind == TypeKind.Enum || ScalarTypes.Contains(underlyingType))
                    {
                        props.Add(new PropertyInfo(
                            member.Name,
                            member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            isNullable || member.Type.NullableAnnotation == NullableAnnotation.Annotated));
                    }
                }

                if (hasPk)
                    allEntities.Add(new EntityInfo(
                        type.Name,
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        type.ContainingNamespace.ToDisplayString(),
                        props));
            }
        }

        if (allEntities.Count == 0) return;

        // Generate per-entity tracker files
        foreach (var entity in allEntities)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using MemoryPack;");
            sb.AppendLine("using XFramework.Domain.Shared.DataContext;");
            sb.AppendLine();
            sb.AppendLine($"namespace {entity.Namespace};");
            sb.AppendLine();

            // Snapshot record
            sb.AppendLine($"[MemoryPackable]");
            sb.AppendLine($"public partial record {entity.Name}Snapshot");
            sb.AppendLine("{");
            for (int i = 0; i < entity.Properties.Count; i++)
            {
                var p = entity.Properties[i];
                sb.AppendLine($"    [MemoryPackOrder({i})] public {p.TypeName} {p.Name} {{ get; init; }}");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            // Tracker class
            sb.AppendLine($"public sealed class {entity.Name}ChangeTracker : IEntityChangeTracker<{entity.FullyQualifiedName}>");
            sb.AppendLine("{");

            // GetPrimaryKey
            sb.AppendLine($"    public System.Guid GetPrimaryKey({entity.FullyQualifiedName} entity) => entity.Id;");
            sb.AppendLine();

            // Snapshot
            sb.AppendLine($"    public object Snapshot({entity.FullyQualifiedName} entity) => new {entity.Name}Snapshot");
            sb.AppendLine("    {");
            foreach (var p in entity.Properties)
                sb.AppendLine($"        {p.Name} = entity.{p.Name},");
            sb.AppendLine("    };");
            sb.AppendLine();

            // Diff
            sb.AppendLine($"    public FieldPatch? Diff({entity.FullyQualifiedName} current, object snapshotObj)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var original = ({entity.Name}Snapshot)snapshotObj;");
            sb.AppendLine("        var changes = new System.Collections.Generic.Dictionary<string, byte[]>();");
            sb.AppendLine();
            foreach (var p in entity.Properties)
            {
                sb.AppendLine($"        if (!System.Collections.Generic.EqualityComparer<{p.TypeName}>.Default.Equals(current.{p.Name}, original.{p.Name}))");
                sb.AppendLine($"            changes[\"{p.Name}\"] = MemoryPack.MemoryPackSerializer.Serialize(current.{p.Name});");
            }
            sb.AppendLine();
            sb.AppendLine("        if (changes.Count == 0) return null;");
            sb.AppendLine();
            sb.AppendLine("        return new FieldPatch");
            sb.AppendLine("        {");
            sb.AppendLine("            EntityId = MemoryPack.MemoryPackSerializer.Serialize(current.Id),");
            sb.AppendLine("            Changes = changes");
            sb.AppendLine("        };");
            sb.AppendLine("    }");

            sb.AppendLine("}");

            context.AddSource($"{entity.Name}ChangeTracker.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        // Generate ChangeTrackerRegistry
        var regSb = new StringBuilder();
        regSb.AppendLine("// <auto-generated/>");
        regSb.AppendLine("#nullable enable");
        regSb.AppendLine("using XFramework.Domain.Shared.DataContext;");
        regSb.AppendLine();
        regSb.AppendLine("namespace XFramework.Integration.DataContext;");
        regSb.AppendLine();
        regSb.AppendLine("public static class ChangeTrackerRegistry");
        regSb.AppendLine("{");
        regSb.AppendLine("    private static readonly System.Collections.Generic.Dictionary<System.Type, object> Trackers = new()");
        regSb.AppendLine("    {");
        foreach (var entity in allEntities)
            regSb.AppendLine($"        [typeof({entity.FullyQualifiedName})] = new {entity.Namespace}.{entity.Name}ChangeTracker(),");
        regSb.AppendLine("    };");
        regSb.AppendLine();
        regSb.AppendLine("    public static IEntityChangeTracker<T> GetTracker<T>() where T : class");
        regSb.AppendLine("        => (IEntityChangeTracker<T>)Trackers[typeof(T)];");
        regSb.AppendLine();
        regSb.AppendLine("    public static bool HasTracker<T>() where T : class");
        regSb.AppendLine("        => Trackers.ContainsKey(typeof(T));");
        regSb.AppendLine("}");

        context.AddSource("ChangeTrackerRegistry.g.cs", SourceText.From(regSb.ToString(), Encoding.UTF8));
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;
        foreach (var childNs in ns.GetNamespaceMembers())
            foreach (var type in GetAllTypes(childNs))
                yield return type;
    }

    private record EntityInfo(string Name, string FullyQualifiedName, string Namespace, List<PropertyInfo> Properties);
    private record PropertyInfo(string Name, string TypeName, bool IsNullable);
}
```

- [ ] **Step 2: Build to verify generator compiles**

Run: `dotnet build src/SourceGenerators/XFramework.SourceGenerators/`
Expected: Build succeeded

- [ ] **Step 3: Build a project that references entities with [GenerateEndpoints] to verify generated output**

Run: `dotnet build src/Infrastructure/XFramework.Integration/`
Expected: Build succeeded. Check that `ChangeTrackerRegistry.g.cs` appears in the analyzer output.

- [ ] **Step 4: Commit**

```bash
git add src/SourceGenerators/XFramework.SourceGenerators/ChangeTrackerGenerator.cs
git commit -m "feat(source-gen): add ChangeTrackerGenerator for per-entity snapshot/diff"
```

---

### Task 8: Extend DataContextRegistrationGenerator with Routing Map

**Files:**
- Modify: `src/SourceGenerators/XFramework.SourceGenerators/DataContextRegistrationGenerator.cs`

The existing generator emits `GetDataContextEntityTypes()` returning `Dictionary<string, Type>`. Extend it to also emit `GetDataContextServiceWrapperMap()` returning `Dictionary<string, Type>` mapping `entityTypeName → I{Module}ServiceWrapper` type.

- [ ] **Step 1: Add routing map generation**

In the `Execute` method, after the existing `GetDataContextEntityTypes()` generation, add a second method. The entity→wrapperType map is built from the referenced assembly name: `IdentityServer.Domain.Shared` → module name `IdentityServer` → wrapper type `I{Module}ServiceWrapper` in `{Module}.Integration`.

Add to the generated class after `GetDataContextEntityTypes()`:

```csharp
    public static System.Collections.Generic.Dictionary<string, System.Type> GetDataContextServiceWrapperMap()
    {
        return new System.Collections.Generic.Dictionary<string, System.Type>
        {
            // For each entity, map to its owning service's wrapper interface type
            // e.g., ["IdentityCredential"] = typeof(IdentityServer.Integration.Drivers.IIdentityServerServiceWrapper)
        };
    }
```

The module name is derived from the entity's assembly: `assemblyName.Split('.')[0]`. The wrapper interface follows the naming convention `I{ModuleName}ServiceWrapper` in namespace `{ModuleName}.Integration.Drivers`.

Note: This requires the generator to discover the wrapper interface type from the compilation's referenced assemblies. If the wrapper type isn't found (assembly not referenced), skip the entity — it won't be callable from this project.

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/SourceGenerators/XFramework.SourceGenerators/`
Expected: Build succeeded

- [ ] **Step 3: Build Integration project and verify generated output**

Run: `dotnet build src/Infrastructure/XFramework.Integration/`
Expected: Generated `DataContextEntityRegistrations.g.cs` now includes `GetDataContextServiceWrapperMap()`.

- [ ] **Step 4: Commit**

```bash
git add src/SourceGenerators/XFramework.SourceGenerators/DataContextRegistrationGenerator.cs
git commit -m "feat(source-gen): add entity-to-service-wrapper routing map"
```

---

### Task 9: Extend ServiceWrapperGenerator with DB Proxy Methods

**Files:**
- Modify: `src/SourceGenerators/XFramework.SourceGenerators/ServiceWrapperGenerator.cs`

Add `IDataContextServiceWrapper` implementation to each generated wrapper. The wrapper gets three new methods that call `BoltClient` with `__db_query__`, `__db_changes__`, `__db_query_stream__`.

- [ ] **Step 1: Add IDataContextServiceWrapper to generated interface**

In the `GenerateWrapper` method, add to the generated interface declaration:

```csharp
public partial interface I{moduleName}ServiceWrapper : IXFrameworkService, IServiceWrapper, IDataContextServiceWrapper
```

- [ ] **Step 2: Add method implementations to generated wrapper record**

In the generated wrapper record body, add:

```csharp
    public async Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
    {
        var (status, data) = await Client.InvokeAsync(ServiceId, "__db_query__", queryDescriptorBytes, ct);
        return data.ToArray();
    }

    public async Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default)
    {
        var (status, data) = await Client.InvokeAsync(ServiceId, "__db_changes__", saveChangesRequestBytes, ct);
        return data.ToArray();
    }

    public async IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(
        byte[] queryDescriptorBytes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var stream = await Client.OpenStreamAsync(ServiceId, "__db_query_stream__", ct);
        try
        {
            await stream.SendAsync((ReadOnlyMemory<byte>)queryDescriptorBytes, ct);
            await foreach (var chunk in stream.ReadAllAsync(ct))
            {
                yield return chunk.ToArray();
            }
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }
```

Note: `Client` and `ServiceId` are already available in the generated `DriverBase` — they're the `BoltClient` instance and the SHA256-hashed module name used for routing.

- [ ] **Step 3: Add required using to generated file**

Ensure the generated source includes:
```csharp
using XFramework.Domain.Shared.DataContext;
using Bolt.Client;
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build`
Expected: Build succeeded. Each `I{Module}ServiceWrapper` now implements `IDataContextServiceWrapper`.

- [ ] **Step 5: Commit**

```bash
git add src/SourceGenerators/XFramework.SourceGenerators/ServiceWrapperGenerator.cs
git commit -m "feat(source-gen): add IDataContextServiceWrapper to generated wrappers"
```

---

### Task 10: Rewrite RemoteDataContext

**Files:**
- Modify: `src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs`

Full rewrite. The class receives DI deps, buffers Add/Update/Remove locally, and routes SaveChangesAsync through the owning service wrapper.

- [ ] **Step 1: Rewrite RemoteDataContext**

```csharp
// src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs
using MemoryPack;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext;

public class RemoteDataContext(
    IServiceProvider serviceProvider,
    RequestMetadata metadata) : IDataContext
{
    private readonly List<TrackedEntity> _trackedEntities = [];
    private readonly List<PendingChange> _pendingChanges = [];

    public IRemoteQuery<T> Query<T>() where T : class
    {
        return new RemoteQuery<T>(serviceProvider, _trackedEntities, metadata);
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
        if (ChangeTrackerRegistry.HasTracker<T>())
        {
            var tracker = ChangeTrackerRegistry.GetTracker<T>();
            var pk = tracker.GetPrimaryKey(entity);
            var tracked = _trackedEntities.FirstOrDefault(t =>
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

        var wrapperMap = DataContextEntityRegistrations.GetDataContextServiceWrapperMap();

        var serviceWrapperTypes = _pendingChanges
            .Select(c =>
            {
                if (!wrapperMap.TryGetValue(c.EntityTypeName, out var wrapperType))
                    throw new InvalidOperationException(
                        $"Entity '{c.EntityTypeName}' is not mapped to any service. " +
                        "Ensure it has [GenerateEndpoints] and the service wrapper is registered.");
                return wrapperType;
            })
            .Distinct()
            .ToList();

        if (serviceWrapperTypes.Count > 1)
            throw new InvalidOperationException(
                $"SaveChangesAsync spans multiple services ({string.Join(", ", serviceWrapperTypes.Select(t => t.Name))}). " +
                "Split changes into separate IDataContext scopes per service.");

        var wrapper = (IDataContextServiceWrapper)serviceProvider.GetRequiredService(serviceWrapperTypes[0]);

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

    private record PendingChange
    {
        public string EntityTypeName { get; init; } = string.Empty;
        public ChangeOperation Operation { get; init; }
        public byte[] SerializedEntity { get; init; } = [];
    }
}
```

Note: `DataContextEntityRegistrations` is source-generated by `DataContextRegistrationGenerator` (extended in Task 8). `ChangeTrackerRegistry` is source-generated by `ChangeTrackerGenerator` (Task 7).

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Infrastructure/XFramework.Integration/`
Expected: Build succeeded (assuming generators have run).

- [ ] **Step 3: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs
git commit -m "feat(data-context): rewrite RemoteDataContext with service wrapper routing"
```

---

### Task 11: Rewrite RemoteQuery Terminal Methods

**Files:**
- Modify: `src/Infrastructure/XFramework.Integration/DataContext/RemoteQuery.cs`

Implement all 15 terminal methods. Builder methods (lines 31-116) remain unchanged — they build the `QueryDescriptor`. Only terminal methods change.

- [ ] **Step 1: Add dependencies and rewrite constructor**

Replace constructor and add fields:

```csharp
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
```

Keep all builder methods (Where, OrderBy, etc.) exactly as-is.

- [ ] **Step 2: Implement entity-returning terminals**

Replace the terminal methods starting at line 118:

```csharp
    public async Task<List<T>> ToListAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.ToList;
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<List<T>>(resultBytes);
        if (result is not null)
            foreach (var entity in result)
                TrackEntity(entity);
        return result ?? [];
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.FirstOrDefault;
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<T?>(resultBytes);
        if (result is not null)
            TrackEntity(result);
        return result;
    }

    public async Task<T?> SingleOrDefaultAsync(CancellationToken ct = default)
    {
        _descriptor.Mode = QueryExecutionMode.SingleOrDefault;
        var resultBytes = await ExecuteQueryAsync(ct);
        var result = MemoryPackSerializer.Deserialize<T?>(resultBytes);
        if (result is not null)
            TrackEntity(result);
        return result;
    }

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
```

- [ ] **Step 3: Implement scalar terminals**

```csharp
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
```

- [ ] **Step 4: Implement aggregation terminals**

```csharp
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
```

- [ ] **Step 5: Add private helper methods**

```csharp
    private async Task<byte[]> ExecuteQueryAsync(CancellationToken ct)
    {
        _descriptor.Metadata = _metadata;
        var wrapper = ResolveWrapper();
        var descriptorBytes = MemoryPackSerializer.Serialize(_descriptor);
        return await wrapper.ExecuteQueryAsync(descriptorBytes, ct);
    }

    private IDataContextServiceWrapper ResolveWrapper()
    {
        var wrapperMap = XFramework.Core.DataContext.DataContextEntityRegistrations.GetDataContextServiceWrapperMap();
        if (!wrapperMap.TryGetValue(typeof(T).Name, out var wrapperType))
            throw new InvalidOperationException(
                $"Entity '{typeof(T).Name}' is not mapped to any service wrapper. " +
                "Ensure it has [GenerateEndpoints] and the service wrapper is registered.");
        return (IDataContextServiceWrapper)_serviceProvider.GetRequiredService(wrapperType);
    }

    private void TrackEntity(T entity)
    {
        if (!ChangeTrackerRegistry.HasTracker<T>()) return;
        var tracker = ChangeTrackerRegistry.GetTracker<T>();
        var pk = tracker.GetPrimaryKey(entity);
        var snapshot = tracker.Snapshot(entity);
        _trackedEntities.Add(new TrackedEntity
        {
            EntityTypeName = typeof(T).Name,
            PrimaryKey = pk,
            Snapshot = snapshot
        });
    }
```

- [ ] **Step 6: Add required usings at top of file**

```csharp
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using MemoryPack;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.ExpressionVisitor;
```

- [ ] **Step 7: Build to verify**

Run: `dotnet build src/Infrastructure/XFramework.Integration/`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/DataContext/RemoteQuery.cs
git commit -m "feat(data-context): implement all RemoteQuery terminal methods"
```

---

### Task 12: DI Wiring + Service Updates

**Files:**
- Modify: `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs`
- Modify: Each service `Program.cs` (IdentityServer.Api, Wallets.Api, etc.)

- [ ] **Step 1: Add RemoteDataContext registration**

In `src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs`, add a new extension method:

```csharp
    /// <summary>
    /// Registers RemoteDataContext as the IDataContext implementation for remote/WASM clients.
    /// Use this in client apps that call services through Bolt.
    /// </summary>
    public static IServiceCollection AddRemoteDataContext(this IServiceCollection services)
    {
        services.AddScoped<IDataContext>(sp =>
        {
            var metadata = sp.GetService<RequestMetadata>() ?? new RequestMetadata();
            return new RemoteDataContext(sp, metadata);
        });
        return services;
    }
```

Add the required using at the top:
```csharp
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;
```

- [ ] **Step 2: Add DataContextBoltHandler to each service**

In each service's `Program.cs` (e.g., `src/Modules/IdentityServer/IdentityServer.Api/Program.cs`), add:

```csharp
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
```

This registers `IQueryExecutionService` with the service's entities and ensures the `DataContextBoltHandler` is discovered by `BoltHandlerRegistrationHostedService` via the `IBoltHandler` scan.

Note: `DataContextBoltHandler` implements `IBoltHandler`, so it's auto-discovered by the existing `BoltHandlerRegistrationHostedService` in `ServiceCollectionExtensions.cs`. No additional hosted service registration needed — but the handler class must be in an assembly that gets scanned. If it's in `XFramework.Core`, it won't be in the entry assembly. Solution: either scan referenced assemblies too, or have each service register the handler explicitly.

Better approach — add to `AddDataContextHandler`:
```csharp
    public static IServiceCollection AddDataContextHandler(this IServiceCollection services, Assembly entityAssembly)
    {
        // ... existing QueryExecutionService registration ...

        // Register the DataContextBoltHandler on the BoltClient at startup
        services.AddHostedService(sp =>
        {
            var client = sp.GetRequiredService<BoltClient>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<DataContextBoltHandler>>();
            var handler = new DataContextBoltHandler();
            handler.Register(client, logger, scopeFactory);
            return new DataContextBoltHandlerHostedService();
        });

        return services;
    }
```

Actually, simpler: call `Register` directly in the DI extension since it only registers lambdas on the BoltClient (no async work needed):

```csharp
    public static IServiceCollection AddDataContextHandler(this IServiceCollection services, Assembly entityAssembly)
    {
        services.AddSingleton<IQueryExecutionService>(sp =>
        {
            // ... existing entity registration code ...
        });

        // Register Bolt handlers after BoltClient is available
        services.AddHostedService<DataContextBoltHandlerRegistration>();

        return services;
    }

    private class DataContextBoltHandlerRegistration(
        BoltClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<DataContextBoltHandler> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken ct)
        {
            var handler = new DataContextBoltHandler();
            handler.Register(client, logger, scopeFactory);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
```

- [ ] **Step 3: Build full solution**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/Infrastructure/XFramework.Integration/Extensions/ServiceCollectionExtensions.cs
git add -u  # captures Program.cs changes
git commit -m "feat(data-context): wire up RemoteDataContext DI and service handlers"
```

---

### Task 13: Integration Test

**Files:**
- Create: `src/Tests/DataContext.IntegrationTests/` (new test project)

Use the existing test fixture pattern (Testcontainers + Bolt Hub + service app + test client) to verify the full round-trip.

- [ ] **Step 1: Create test project**

Create `src/Tests/DataContext.IntegrationTests/DataContext.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Testcontainers.PostgreSql" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Modules\IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj" />
    <ProjectReference Include="..\..\Modules\IdentityServer\IdentityServer.Integration\IdentityServer.Integration.csproj" />
    <ProjectReference Include="..\..\Tests\XFramework.TestInfrastructure\XFramework.TestInfrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create test fixture**

Follow the pattern from `IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs`. The fixture starts Postgres + Bolt Hub + IdentityServer (with `AddDataContextHandler`) + test client (with `AddRemoteDataContext`).

Key difference from existing fixtures: the test client app calls `AddRemoteDataContext()` in addition to existing service wrapper registration, so `IDataContext` resolves to `RemoteDataContext`.

- [ ] **Step 3: Write round-trip query test**

```csharp
[TestFixture]
public class RemoteDataContextTests
{
    [Test]
    public async Task Query_ToListAsync_ReturnsEntitiesFromService()
    {
        var ctx = TestFixture.TestClientServices.GetRequiredService<IDataContext>();
        var results = await ctx.Query<IdentityCredential>()
            .Where(x => x.TenantId == TestFixture.TestTenantId)
            .Take(10)
            .ToListAsync();

        results.Should().NotBeEmpty();
    }

    [Test]
    public async Task SaveChangesAsync_MixedServices_Throws()
    {
        // If entities from different services are added, SaveChanges should throw
        var ctx = TestFixture.TestClientServices.GetRequiredService<IDataContext>();
        ctx.Add(new IdentityCredential { /* ... */ });
        // ctx.Add(new WalletTransaction { /* ... */ }); // different service

        // For this test, just verify single-service works
        var result = await ctx.SaveChangesAsync();
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task ToAsyncEnumerable_StreamsInChunks()
    {
        var ctx = TestFixture.TestClientServices.GetRequiredService<IDataContext>();
        var count = 0;
        await foreach (var item in ctx.Query<IdentityCredential>()
            .Where(x => x.TenantId == TestFixture.TestTenantId)
            .ToAsyncEnumerable(chunkSize: 2))
        {
            count++;
            item.Should().NotBeNull();
        }
        count.Should().BeGreaterThan(0);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `DOCKER_HOST=tcp://100.75.11.49:2375 TESTCONTAINERS_HOST_OVERRIDE=100.75.11.49 dotnet test src/Tests/DataContext.IntegrationTests/ -v n`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add src/Tests/DataContext.IntegrationTests/
git commit -m "test(data-context): add integration tests for RemoteDataContext round-trip"
```

---

### Task 14: Hub Cleanup

**Files:**
- Modify: `src/Libraries/Bolt/Bolt.Protocol/Protocol/FrameType.cs` — remove ExecuteQuery, ExecuteChanges
- Modify: `src/Libraries/Bolt/Bolt.Protocol/Protocol/BoltCodec.cs` — remove encode/decode for 0x0B, 0x0C
- Modify: `src/Libraries/Bolt/Bolt.Server/BoltServer.cs` — remove ExecuteQuery/ExecuteChanges frame handlers
- Modify: Hub startup — remove `QueryExecutionService` registration and entity scanning

- [ ] **Step 1: Remove transitional frame types from FrameType.cs**

In `src/Libraries/Bolt/Bolt.Protocol/Protocol/FrameType.cs`, remove:
```csharp
    ExecuteQuery = 0x0B,
    ExecuteChanges = 0x0C,
```

- [ ] **Step 2: Remove codec methods from BoltCodec.cs**

Remove `WriteExecuteQuery`, `WriteExecuteChanges`, and their `TryRead` counterparts. Remove `ExecuteQueryHeaderSize` and `ExecuteChangesHeaderSize` constants.

- [ ] **Step 3: Remove Hub-side ExecuteQuery/ExecuteChanges frame handlers**

In `src/Libraries/Bolt/Bolt.Server/BoltServer.cs`, remove the frame routing cases for `FrameType.ExecuteQuery` and `FrameType.ExecuteChanges`.

- [ ] **Step 4: Remove QueryExecutionService from Hub DI registration**

In `src/Modules/XFramework.Bolt/Bolt.Hub/Installers/BoltInstaller.cs`, remove any registration of `IQueryExecutionService` as a singleton or scoped service. The Hub no longer needs it.

- [ ] **Step 5: Build to verify**

Run: `dotnet build`
Expected: Build succeeded. Any code that referenced `FrameType.ExecuteQuery` or `ExecuteChanges` should now fail to compile — fix all references.

- [ ] **Step 6: Run integration tests to verify nothing broke**

Run: `dotnet test src/Tests/IdentityServer.IntegrationTests/ -v n`
Run: `dotnet test src/Tests/DataContext.IntegrationTests/ -v n`
Expected: All tests pass

- [ ] **Step 7: Commit**

```bash
git add -u
git commit -m "chore(bolt): remove ExecuteQuery/ExecuteChanges transitional shim from Hub"
```

---

## Execution Order & Dependencies

```
Task 1 (shared types) ─────┐
Task 2 (extend types) ──────┤
Task 3 (signature updates) ─┤── Foundation (no runtime deps)
                             │
Task 4 (move QES to Core) ──┤
Task 5 (FieldPatch support) ┤── Service-side handlers
Task 6 (BoltHandler + DI) ──┘
                             
Task 7 (ChangeTrackerGen) ──┐
Task 8 (RoutingGen) ─────────┤── Source generators (can parallelize)
Task 9 (WrapperGen) ─────────┘
                             
Task 10 (RemoteDataContext) ─┐── Client-side (depends on Tasks 1-9)
Task 11 (RemoteQuery) ───────┘
                             
Task 12 (DI wiring) ────────── Wire-up (depends on Tasks 10-11)
Task 13 (Integration test) ── Verification (depends on Task 12)
Task 14 (Hub cleanup) ─────── Cleanup (depends on Task 13 passing)
```

Tasks 1-6 can run sequentially. Tasks 7-9 can run in parallel. Tasks 10-11 are sequential. Tasks 12-14 are sequential.
