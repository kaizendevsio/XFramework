---
title: "Decentralized Remote Data Context"
date: 2026-04-22
category: architecture-patterns
module: XFramework.Core
problem_type: architecture_pattern
component: database
severity: critical
applies_when:
  - "Routing remote IDataContext queries and SaveChanges calls through owning services instead of a centralized Bolt Hub database proxy"
tags: [datacontext, remote-query, bolt, service-wrapper, source-generators]
---

# DB Proxy Decentralization: Per-Service Data Context

**Date:** 2026-04-22
**Status:** Draft
**Scope:** Move query execution from the centralized Bolt Hub into individual services, so each service owns its data. `IDataContext` works identically regardless of runtime environment (Blazor Server, WASM, MAUI, etc.) — the remote implementation calls the owning service through generated service wrappers.

## Goal

Make `IDataContext` a truly universal data access API. When running in-process (Blazor Server), it uses EF Core directly via `ServerDataContext`. When running remotely (WASM, MAUI, or any client), `RemoteDataContext` routes queries to the owning service through the existing Bolt RPC + service wrapper infrastructure.

The Bolt Hub stops being a DB proxy. Each service registers a generic query handler that executes LINQ queries against its own DbContext. The Hub's only job is message routing.

## Non-Goals

- **Cross-service transactions** — A single `SaveChangesAsync` must target one service. If the ChangeSet contains entities from multiple services, it throws. No 2PC / saga orchestration.
- **New Bolt frame types** — Uses existing Request/Response frames. The transitional `ExecuteQuery` (0x0B) and `ExecuteChanges` (0x0C) shim frames on the Hub are removed.
- **Wildcard / dynamic query routing** — Routing is static, determined at compile time by source generators.
- **Client-side LINQ provider** — We serialize the query descriptor built by `RemoteQuery<T>`, not raw expression trees. The existing `QueryExpressionVisitor` + `QueryDescriptorExecutor` pipeline is reused.

## Architecture

### Before (Centralized Hub Proxy)

```
Client App (WASM/MAUI)
    ↓ IDataContext
RemoteDataContext           ← throws NotImplementedException today
    ↓ Bolt RPC (FrameType.ExecuteQuery / ExecuteChanges)
Bolt Hub
    ↓ QueryExecutionService  ← Hub owns a DbContext, executes ALL queries
Single shared DbContext
    ↓ EF Core
PostgreSQL
```

**Problem:** Hub is a single point of failure for all DB queries. It needs connection strings and DbContext for every module. It becomes a bottleneck under load.

### After (Decentralized Per-Service)

```
Client App (WASM/MAUI)
    ↓ IDataContext
RemoteDataContext
    ↓ IRemoteQuery<T>.ToListAsync()
    ↓ looks up entity → serviceId from source-generated routing map
    ↓ resolves I{Module}ServiceWrapper from DI
    ↓ wrapper.ExecuteQueryAsync(descriptor) / wrapper.ExecuteChangesAsync(changeSet)
    ↓ Bolt RPC (standard Request/Response frames)
Bolt Hub
    ↓ routes to owning service (zero knowledge of DB)
IdentityServer / Wallets / etc.
    ↓ __db_query__ / __db_changes__ handler
    ↓ QueryDescriptorExecutor (reused from Hub)
Service's own DbContext
    ↓ EF Core
PostgreSQL
```

**Result:** Each service owns its data. Hub is a pure message router. `IDataContext` callers don't know or care.

### In-Process Path (Unchanged)

```
Blazor Server / Background Service
    ↓ IDataContext
ServerDataContext<AppDbContext>
    ↓ EF Core (direct)
PostgreSQL
```

No changes. `ServerDataContext` and `ServerQuery<T>` remain as-is.

## Design Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | **Routing strategy** | Static compile-time map | Source generator builds `entityTypeName → serviceId` from `[GenerateEndpoints]` attributes. Zero runtime discovery. |
| 2 | **Service handler exposure** | Single generic handler per service | Each service registers `__db_query__` and `__db_changes__` handlers. One registration, handles all entities owned by that service. |
| 3 | **Query wire format** | `QueryDescriptor` envelope | Existing `QueryDescriptor` (13 properties, MemoryPackable) carries the full query: entity type, filters, sorts, includes, skip/take, execution mode. Serialized via MemoryPack. |
| 4 | **Write wire format** | Buffered `ChangeSet` on SaveChangesAsync | Client buffers Add/Update/Remove locally. `SaveChangesAsync` ships a single `SaveChangesRequest` (existing type) with `List<ChangeEntry>`. Throw if entities span multiple services. |
| 5 | **Update semantics** | EF-style snapshot tracking with field-level diffs | On query materialization, client snapshots each entity. On save, diffs current vs snapshot, ships only changed fields. Supports concurrency tokens. |
| 6 | **Change tracking impl** | Source-generated per-entity trackers | `{Entity}ChangeTracker` generated alongside endpoints. Hand-rolled property comparers. Zero reflection, zero proxies, works with records. |
| 7 | **Result transport** | Single Response payload | All terminal methods (ToListAsync, CountAsync, etc.) return via standard Bolt Request/Response. No protocol changes. |
| 8 | **Streaming queries** | `ToAsyncEnumerable(chunkSize)` via BoltStream | Uses existing reliable StreamData frames (TCP/WebSocket). Chunks results server-side, streams to client via `IAsyncEnumerable<T>`. |
| 9 | **Tenant/auth context** | `RequestMetadata` in payload | Same pattern as every other XFramework request. QueryDescriptor and SaveChangesRequest carry `RequestMetadata` (TenantId, RequestId). Service applies tenant filtering. |
| 10 | **Concurrency conflicts** | Throw typed exception to caller | Service detects conflict via EF concurrency tokens, returns error with conflict details. Client-side `SaveChangesAsync` throws `DataContextConcurrencyException`. Caller decides resolution. |
| 11 | **Include / navigation** | Full support via expression visitor | QueryDescriptor's operation list captures Include/ThenInclude. Service reconstructs IQueryable with includes. MemoryPack handles object graph serialization with reference preservation. |
| 12 | **Transport** | Via service wrappers | Source generator adds `ExecuteQueryAsync` / `ExecuteChangesAsync` / `ExecuteQueryStreamAsync` to each `I{Module}ServiceWrapper`. `RemoteDataContext` resolves the wrapper from DI. |

## Existing Infrastructure (Reused As-Is)

These types already exist and work. No changes needed:

| Type | Location | Purpose |
|------|----------|---------|
| `IDataContext` | `XFramework.Domain.Shared/DataContext/` | 5-method interface (Query, Add, Update, Remove, SaveChangesAsync) |
| `IRemoteQuery<T>` | `XFramework.Domain.Shared/DataContext/` | 22 methods: 11 builders + 11 terminals |
| `ServerDataContext<T>` | `XFramework.Core/DataContext/` | Direct EF Core implementation — untouched |
| `ServerQuery<T>` | `XFramework.Core/DataContext/` | IQueryable pipeline — untouched |
| `QueryDescriptor` | `XFramework.Domain.Shared/DataContext/` | MemoryPackable query envelope (13 properties) |
| `SaveChangesRequest` | `XFramework.Domain.Shared/DataContext/` | MemoryPackable, holds `List<ChangeEntry>` |
| `ChangeEntry` | `XFramework.Domain.Shared/DataContext/` | EntityTypeName + ChangeOperation + byte[] SerializedEntity |
| `QueryDescriptorExecutor` | `XFramework.Core/DataContext/` | 467-line static class reconstructing IQueryable from QueryDescriptor. Supports all 15 execution modes. |
| `QueryExpressionVisitor` | `XFramework.Integration/DataContext/ExpressionVisitor/` | Converts lambda expressions to `List<QueryFilter>` |
| `CachingDataContext` | `XFramework.Integration/DataContext/` | Decorator over IDataContext with auto-invalidation |
| `CachingQuery<T>` | `XFramework.Integration/DataContext/` | Decorator over IRemoteQuery caching terminal results |
| `BoltStream` | `Bolt.Client/` | Reliable TCP streaming with `IAsyncEnumerable<T>` support |
| `DataContextResult` | `XFramework.Domain.Shared/DataContext/` | Success/Failure result type |

## Routing Map (Source Generator)

### Current: `DataContextRegistrationGenerator`

Generates `Dictionary<string, Type>` mapping entity name → Type. Used by the Hub's `QueryExecutionService` to validate entity access.

### New: `DataContextRoutingGenerator`

Extends the existing generator to also produce a `Dictionary<string, string>` mapping `entityTypeName → serviceId`. The service ID comes from the project context where `[GenerateEndpoints]` is applied:

- IdentityServer.Api defines `[GenerateEndpoints]` on `IdentityCredential` → entity routes to `"IdentityServer"`
- Wallets.Api defines `[GenerateEndpoints]` on `WalletTransaction` → entity routes to `"Wallets"`

The generator discovers the service name from the assembly name convention (`{Module}.Api` → service ID is `{Module}`), or from a new optional `ServiceName` property on `[GenerateEndpoints]` for overrides.

Generated output (in each Integration project):

```csharp
// Auto-generated by DataContextRoutingGenerator
namespace XFramework.Integration.DataContext;

public static class DataContextRouting
{
    public static IReadOnlyDictionary<string, string> EntityServiceMap { get; } =
        new Dictionary<string, string>
        {
            ["IdentityCredential"] = "IdentityServer",
            ["IdentityRole"] = "IdentityServer",
            ["WalletTransaction"] = "Wallets",
            ["WalletType"] = "Wallets",
            // ... all entities from all referenced [GenerateEndpoints] assemblies
        };
}
```

`RemoteDataContext` reads this map at construction (injected or static) to resolve the correct service wrapper per entity type.

## Service Wrapper Extensions (Source Generator)

The existing `ServiceWrapperGenerator` is extended to emit three additional methods on each `I{Module}ServiceWrapper`:

```csharp
public partial interface IIdentityServerServiceWrapper
{
    // Existing: per-entity CRUD + custom Bolt request methods
    // ...

    // New: generic DB proxy methods
    Task<QueryResponse<byte[]>> ExecuteQueryAsync(QueryDescriptor descriptor);
    Task<CmdResponse<DataContextResult>> ExecuteChangesAsync(SaveChangesRequest request);
    IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(QueryDescriptor descriptor);
}
```

The implementation delegates to `SendAsync` / `SendVoidAsync` with well-known command names:
- `__db_query__` → `ExecuteQueryAsync`
- `__db_changes__` → `ExecuteChangesAsync`
- `__db_query_stream__` → `ExecuteQueryStreamAsync` (uses BoltStream)

## RemoteDataContext (Rewrite)

Currently: every method throws `NotImplementedException`.

After: fully functional, routes through service wrappers.

```csharp
public class RemoteDataContext : IDataContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, string> _entityServiceMap;
    private readonly RequestMetadata _metadata;
    private readonly List<TrackedEntity> _trackedEntities = [];
    private readonly List<ChangeEntry> _pendingChanges = [];

    public IRemoteQuery<T> Query<T>() where T : class
    {
        return new RemoteQuery<T>(_serviceProvider, _entityServiceMap, _metadata, _trackedEntities);
    }

    public void Add<T>(T entity) where T : class
    {
        _pendingChanges.Add(new ChangeEntry
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Add,
            SerializedEntity = MemoryPackSerializer.Serialize(entity)
        });
    }

    public void Update<T>(T entity) where T : class
    {
        // Source-generated tracker computes field-level diff
        var tracker = ChangeTrackerRegistry.GetTracker<T>();
        var snapshot = _trackedEntities.FindSnapshot(entity);
        var diff = tracker.Diff(entity, snapshot);

        _pendingChanges.Add(new ChangeEntry
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Update,
            SerializedEntity = MemoryPackSerializer.Serialize(diff)
        });
    }

    public void Remove<T>(T entity) where T : class
    {
        _pendingChanges.Add(new ChangeEntry
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Remove,
            SerializedEntity = MemoryPackSerializer.Serialize(entity)
        });
    }

    public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_pendingChanges.Count == 0) return DataContextResult.Success();

        // Validate all changes target one service
        var services = _pendingChanges
            .Select(c => _entityServiceMap[c.EntityTypeName])
            .Distinct()
            .ToList();

        if (services.Count > 1)
            throw new InvalidOperationException(
                $"SaveChangesAsync spans multiple services ({string.Join(", ", services)}). " +
                "Split changes into separate IDataContext scopes per service.");

        var wrapper = ResolveWrapper(services[0]);
        var request = new SaveChangesRequest
        {
            Metadata = _metadata,
            Changes = _pendingChanges
        };

        var result = await wrapper.ExecuteChangesAsync(request);
        if (result.IsSuccess) _pendingChanges.Clear();
        return result;
    }
}
```

## RemoteQuery (Terminal Methods Rewrite)

Currently: builder methods work (build `QueryDescriptor`), all terminals throw.

After: terminals call the service wrapper.

```csharp
public class RemoteQuery<T> : IRemoteQuery<T> where T : class
{
    // Builder methods remain unchanged — they populate this.Descriptor

    public async Task<List<T>> ToListAsync(CancellationToken ct = default)
    {
        Descriptor.ExecutionMode = QueryExecutionMode.ToList;
        var wrapper = ResolveWrapper();
        var bytes = await wrapper.ExecuteQueryAsync(Descriptor);
        var result = MemoryPackSerializer.Deserialize<List<T>>(bytes.Response);

        // Snapshot each entity for change tracking
        foreach (var entity in result)
            TrackEntity(entity);

        return result;
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        Descriptor.ExecutionMode = QueryExecutionMode.FirstOrDefault;
        var wrapper = ResolveWrapper();
        var bytes = await wrapper.ExecuteQueryAsync(Descriptor);
        var result = MemoryPackSerializer.Deserialize<T?>(bytes.Response);

        if (result != null) TrackEntity(result);
        return result;
    }

    // CountAsync, AnyAsync, SumAsync, etc. — same pattern, no tracking needed for scalars

    public async IAsyncEnumerable<T> ToAsyncEnumerable(
        int chunkSize = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Descriptor.ExecutionMode = QueryExecutionMode.Stream;
        Descriptor.ChunkSize = chunkSize;
        var wrapper = ResolveWrapper();

        await foreach (var chunk in wrapper.ExecuteQueryStreamAsync(Descriptor).WithCancellation(ct))
        {
            var items = MemoryPackSerializer.Deserialize<List<T>>(chunk);
            foreach (var item in items)
            {
                TrackEntity(item);
                yield return item;
            }
        }
    }

    private void TrackEntity(T entity)
    {
        var tracker = ChangeTrackerRegistry.GetTracker<T>();
        var snapshot = tracker.Snapshot(entity);
        _trackedEntities.Add(new TrackedEntity(typeof(T).Name, GetPrimaryKey(entity), snapshot));
    }

    private IServiceWrapper ResolveWrapper()
    {
        var serviceId = _entityServiceMap[typeof(T).Name];
        // Resolve I{Module}ServiceWrapper from DI by convention
        return _serviceProvider.GetRequiredService(serviceId);
    }
}
```

## Source-Generated Change Trackers

For each entity with `[GenerateEndpoints]`, the generator emits a tracker:

```csharp
// Auto-generated: IdentityCredentialChangeTracker.g.cs
[MemoryPackable]
public partial record IdentityCredentialSnapshot
{
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public Guid? RoleId { get; init; }
    // ... all non-PK, non-nav properties
}

public sealed class IdentityCredentialChangeTracker : IEntityChangeTracker<IdentityCredential>
{
    public IdentityCredentialSnapshot Snapshot(IdentityCredential entity) => new()
    {
        UserName = entity.UserName,
        Email = entity.Email,
        RoleId = entity.RoleId,
    };

    public ChangeEntry? Diff(IdentityCredential current, IdentityCredentialSnapshot original)
    {
        var changes = new Dictionary<string, byte[]>();

        if (current.UserName != original.UserName)
            changes["UserName"] = MemoryPackSerializer.Serialize(current.UserName);
        if (current.Email != original.Email)
            changes["Email"] = MemoryPackSerializer.Serialize(current.Email);
        if (current.RoleId != original.RoleId)
            changes["RoleId"] = MemoryPackSerializer.Serialize(current.RoleId);

        if (changes.Count == 0) return null; // No changes

        return new ChangeEntry
        {
            EntityTypeName = "IdentityCredential",
            Operation = ChangeOperation.Update,
            SerializedEntity = MemoryPackSerializer.Serialize(new FieldPatch
            {
                EntityId = MemoryPackSerializer.Serialize(current.Id),
                Changes = changes
            })
        };
    }
}
```

### ChangeTrackerRegistry

Also source-generated — a static lookup:

```csharp
public static class ChangeTrackerRegistry
{
    private static readonly Dictionary<Type, object> Trackers = new()
    {
        [typeof(IdentityCredential)] = new IdentityCredentialChangeTracker(),
        [typeof(WalletTransaction)] = new WalletTransactionChangeTracker(),
        // ...
    };

    public static IEntityChangeTracker<T> GetTracker<T>() where T : class
        => (IEntityChangeTracker<T>)Trackers[typeof(T)];
}
```

## Service-Side Handler

Each service registers a handler for `__db_query__` and `__db_changes__`. This is a shared base class in `XFramework.Core`:

```csharp
public class DataContextBoltHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQueryExecutionService _queryService;

    public async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleQuery(
        ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var descriptor = MemoryPackSerializer.Deserialize<QueryDescriptor>(payload.Span)!;
        // Reuses existing QueryDescriptorExecutor
        var result = await _queryService.ExecuteAsync(descriptor);
        return (HttpStatusCode.OK, result);
    }

    public async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleChanges(
        ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var request = MemoryPackSerializer.Deserialize<SaveChangesRequest>(payload.Span)!;
        var result = await _queryService.ExecuteChangesAsync(request);
        return (HttpStatusCode.OK, result);
    }

    public async IAsyncEnumerable<byte[]> HandleQueryStream(
        ReadOnlyMemory<byte> payload, Guid requestId)
    {
        var descriptor = MemoryPackSerializer.Deserialize<QueryDescriptor>(payload.Span)!;
        await foreach (var chunk in _queryService.ExecuteStreamAsync(descriptor))
        {
            yield return chunk;
        }
    }
}
```

Each service's startup registers entity types with `IQueryExecutionService` (same as Hub does today) and registers the Bolt handlers:

```csharp
// In IdentityServer.Api Program.cs (or auto-generated)
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
```

This scans `[GenerateEndpoints]` entities in the assembly, registers them with `QueryExecutionService`, and registers the `__db_query__` / `__db_changes__` / `__db_query_stream__` Bolt handlers.

## New Wire Types

### FieldPatch (for Update diffs)

```csharp
[MemoryPackable]
public partial record FieldPatch
{
    public byte[] EntityId { get; init; }
    public Dictionary<string, byte[]> Changes { get; init; } // fieldName → serialized value
}
```

### DataContextConcurrencyException

```csharp
public class DataContextConcurrencyException : Exception
{
    public string EntityTypeName { get; init; }
    public byte[] EntityId { get; init; }
    public Dictionary<string, byte[]> CurrentDbValues { get; init; }
    public Dictionary<string, byte[]> ClientValues { get; init; }
}
```

### QueryDescriptor Extension

Add one optional property to existing `QueryDescriptor`:

```csharp
// Already exists — add:
[MemoryPackOrder(13)]
public int? ChunkSize { get; set; }
```

## Concurrency Token Handling

When the service-side handler applies a `FieldPatch` Update:

1. Load the entity by PK from EF
2. Check each field in the patch — if the entity has a `[ConcurrencyCheck]` or `[Timestamp]` property, verify the client's original value (from snapshot, included in the patch) matches the current DB value
3. If mismatch → return error with `DataContextConcurrencyException` details serialized in the Response
4. If match → apply the patch fields, `SaveChangesAsync`

EF Core's built-in concurrency checking handles most of this automatically via the `RowVersion`/`ConcurrencyToken` mechanism when the entity is loaded and modified through EF's change tracker.

## Migration Path

### Phase 1: Service-side handlers

- Add `DataContextBoltHandler` base class to `XFramework.Core`
- Add `AddDataContextHandler()` extension method
- Each service registers it in startup
- Hub's `QueryExecutionService` remains as fallback

### Phase 2: Client-side rewrite

- Rewrite `RemoteDataContext` to route through service wrappers
- Rewrite `RemoteQuery<T>` terminal methods
- Source generator emits routing map + change trackers
- `CachingDataContext` / `CachingQuery` continue to work as decorators (unchanged)

### Phase 3: Hub cleanup

- Remove `QueryExecutionService` from Hub
- Remove `ExecuteQuery` (0x0B) and `ExecuteChanges` (0x0C) transitional frame types from `FrameType` enum and `BoltCodec`
- Hub becomes a pure message router

## Critical Files

### Modified

| File | Change |
|------|--------|
| `XFramework.Integration/DataContext/RemoteDataContext.cs` | Full rewrite — route through service wrappers |
| `XFramework.Integration/DataContext/RemoteQuery.cs` | Implement all terminal methods |
| `XFramework.Domain.Shared/DataContext/QueryDescriptor.cs` | Add `ChunkSize` property |
| `XFramework.Domain.Shared/DataContext/ChangeEntry.cs` | Support `FieldPatch` for Update diffs |
| `SourceGenerators/DataContextRegistrationGenerator.cs` | Extend to emit routing map (entity → serviceId) |
| `SourceGenerators/ServiceWrapperGenerator.cs` | Emit `ExecuteQueryAsync` / `ExecuteChangesAsync` / `ExecuteQueryStreamAsync` |
| Each service's `Program.cs` | Add `builder.Services.AddDataContextHandler(assembly)` |

### New

| File | Purpose |
|------|---------|
| `XFramework.Core/DataContext/DataContextBoltHandler.cs` | Shared handler base class |
| `XFramework.Core/DataContext/AddDataContextHandlerExtension.cs` | DI registration helper |
| `XFramework.Domain.Shared/DataContext/FieldPatch.cs` | MemoryPackable field-level diff |
| `XFramework.Domain.Shared/DataContext/DataContextConcurrencyException.cs` | Typed concurrency conflict |
| `XFramework.Domain.Shared/DataContext/IEntityChangeTracker.cs` | Interface for generated trackers |
| `SourceGenerators/ChangeTrackerGenerator.cs` | New generator: per-entity snapshot + diff + registry |

### Removed (Phase 3)

| File | Reason |
|------|--------|
| `Bolt.Hub/Services/QueryExecutionService.cs` | Centralized proxy no longer needed |
| `FrameType.ExecuteQuery` / `ExecuteChanges` | Transitional shim removed |
| `BoltCodec` ExecuteQuery/Changes encode/decode | Dead code after shim removal |

## Verification

1. **Unit test**: `RemoteQuery<T>` builds correct `QueryDescriptor` for each terminal method (already partially tested via builder methods)
2. **Unit test**: Source-generated change tracker snapshots and diffs correctly for each entity
3. **Integration test**: `RemoteDataContext.Query<IdentityCredential>().Where(x => x.UserName == "test").ToListAsync()` routes through IdentityServer's `__db_query__` handler and returns data
4. **Integration test**: `RemoteDataContext` Add + SaveChangesAsync creates a record via the service
5. **Integration test**: Update with concurrency conflict throws `DataContextConcurrencyException`
6. **Integration test**: `ToAsyncEnumerable(chunkSize: 10)` streams 100 records in 10 chunks via BoltStream
7. **Integration test**: Mixed-service SaveChangesAsync throws with clear error message
8. **Benchmark**: Compare RemoteDataContext round-trip latency vs direct HTTP endpoint for same query
