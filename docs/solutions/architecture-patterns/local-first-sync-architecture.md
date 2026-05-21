---
title: "XFramework Local-First Sync Architecture"
date: 2026-03-13
status: historical-proposed
superseded_by: "docs/solutions/architecture-patterns/decentralized-remote-data-context.md"
category: architecture-patterns
module: XFramework.Blazor
problem_type: architecture_pattern
component: database
severity: medium
applies_when:
  - "Designing a unified Blazor Server and WebAssembly data-access experience with local replicas and server authority"
tags: [local-first, sync, blazor, wasm, datacontext, streamflow]
---

# XFramework Local-First Sync Architecture

**Status:** Historical proposed design, not current implementation guidance
**Date:** 2026-03-13
**Scope:** Unified Blazor Server + WASM developer experience via local-first data sync

This document preserves a StreamFlow/SignalR-era proposal. Current XFramework uses Bolt for module RPC/streaming and `IDataContext` remote routing through generated service wrappers. Use `docs/solutions/architecture-patterns/decentralized-remote-data-context.md` and `docs/solutions/conventions/ef-core-data-access-patterns.md` for current data-context guidance.

---

## 1. Problem Statement

XFramework targets both Blazor Server and Blazor WASM hosting models. Today:

- **Blazor Server** modules query `DbContext` directly (shared database, same process).
- **Blazor WASM** uses `IServiceWrapper` interfaces over StreamFlow/SignalR for every data operation.

This means developers must write different data-access code depending on the hosting model. The goal is a **unified developer experience** where the same `DbContext`-based code works identically on both Server and WASM.

### Design Principles

1. **Security first** — binary serialization (MessagePack) over WebSocket; no inspectable REST traffic for framework consumers.
2. **Server is authority** — the server database is the source of truth. Local state is a replica, never authoritative.
3. **Optimistic UI** — writes apply locally immediately, then confirm/reject via server.
4. **Minimal developer friction** — same `DbContext` API regardless of hosting model.
5. **Sync over StreamFlow** — reuse the existing SignalR infrastructure; no new transport layer.

---

## 2. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     Application Code                         │
│           (Features, Services, Behaviors)                    │
│         Uses DbContext / IAppDbContext uniformly              │
├──────────────────────────────────────────────────────────────┤
│                    IAppDbContext                              │
│              (Shared abstraction layer)                       │
├─────────────────────────┬────────────────────────────────────┤
│     Blazor Server       │          Blazor WASM               │
│                         │                                    │
│   ServerDbContext        │   LocalDbContext                   │
│   Provider: SQL/PG      │   Provider: SQLite (WASM)          │
│   Direct DB access      │   Local replica of authorized data │
│                         │                                    │
│   Access control:       │   SyncEngine (background)          │
│   Global query filters  │   ├─ Outbox: queued local writes   │
│   + service-layer       │   ├─ Inbox: server-pushed changes  │
│   validation            │   └─ Conflict resolution           │
│                         │                                    │
│                         │   StreamFlow/SignalR connection     │
│                         │   ↕ Binary (MessagePack)           │
└─────────────────────────┴────────────────────────────────────┘
                                    ↕
                          ┌─────────────────────┐
                          │   Server Sync Hub    │
                          │   (StreamFlow)       │
                          │                      │
                          │   AuthZ gate:        │
                          │   - validates writes  │
                          │   - scopes reads     │
                          │   - pushes deltas    │
                          └──────────┬──────────┘
                                     ↕
                          ┌─────────────────────┐
                          │  Server Database     │
                          │  (SQL Server / PG)   │
                          │  Source of truth      │
                          └─────────────────────┘
```

---

## 3. Core Components

### 3.1 IAppDbContext (Shared Interface)

Both hosting models expose the same data-access surface:

```csharp
public interface IAppDbContext : IAsyncDisposable
{
    DbSet<T> Set<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

Application code depends only on `IAppDbContext`. DI resolves the appropriate implementation based on the hosting model.

### 3.2 ServerDbContext (Blazor Server)

Standard EF Core `DbContext` configured with the real database provider. No sync needed.

```csharp
public class ServerDbContext : AppDbContext, IAppDbContext
{
    // Configured with UseSqlServer() or UseNpgsql()
    // Global query filters for tenant + credential scoping
}
```

### 3.3 LocalDbContext (Blazor WASM)

EF Core `DbContext` configured with SQLite running in the browser via WASM.

```csharp
public class LocalDbContext : AppDbContext, IAppDbContext
{
    // Configured with UseSqlite("Data Source=local.db") — in-browser
    // Same entity models and global query filters as ServerDbContext
    // SaveChangesAsync override: persist locally + queue to outbox
}
```

**Technology:** .NET 8+ added experimental SQLite WASM support via `Microsoft.EntityFrameworkCore.Sqlite` with `e_sqlite3` compiled to WASM. .NET 10 should have mature support.

### 3.4 SyncEngine (WASM Only)

Background service that manages bidirectional data flow over StreamFlow:

```csharp
public class SyncEngine : IAsyncDisposable
{
    // Dependencies
    private readonly LocalDbContext _localDb;
    private readonly IMessageBusWrapper _streamFlow;

    // Lifecycle
    Task InitializeAsync(Guid credentialId, Guid tenantId);
    Task StartAsync(CancellationToken ct);
    Task StopAsync();

    // Sync operations
    Task<SyncResult> PushLocalChangesAsync();      // Outbox → Server
    Task ApplyServerChangesAsync(SyncDelta delta);  // Server → Local
    Task<SyncSnapshot> RequestSnapshotAsync();       // Initial load
}
```

### 3.5 Server Sync Hub (StreamFlow Extension)

Server-side handler that processes sync requests within the existing StreamFlow hub infrastructure:

```csharp
public class SyncRequestHandler
{
    // Initial sync: send authorized data snapshot
    Task<SyncSnapshot> HandleSnapshotRequest(SnapshotRequest request, SyncContext ctx);

    // Client push: validate and apply a batch of changes
    Task<SyncResult> HandlePushChanges(ChangeSet changes, SyncContext ctx);

    // Server push: broadcast authorized deltas to connected clients
    Task BroadcastDelta(SyncDelta delta, Func<Guid, bool> authFilter);
}
```

---

## 4. Sync Protocol

### 4.1 Initial Sync (Snapshot)

```
Client                          Server
  │                               │
  │─── SnapshotRequest ──────────→│  (credentialId, tenantId, lastSyncVersion)
  │                               │
  │                               │  Query authorized data:
  │                               │  - Apply tenant filter
  │                               │  - Apply credential-level access
  │                               │  - Include only entities user can read
  │                               │
  │←── SyncSnapshot ─────────────│  (entities[], syncVersion, schemaVersion)
  │                               │
  │  Apply to local SQLite        │
  │  Set local syncVersion        │
```

### 4.2 Client → Server (Push)

```
Client                          Server
  │                               │
  │  Local write occurs           │
  │  SaveChangesAsync() →         │
  │    1. Persist to SQLite       │
  │    2. Queue to Outbox table   │
  │                               │
  │─── ChangeSet ────────────────→│  (changes[], clientSyncVersion)
  │                               │
  │                               │  For each change:
  │                               │    1. Authorize (can user write this?)
  │                               │    2. Validate (business rules)
  │                               │    3. Apply to server DB
  │                               │    4. Assign server syncVersion
  │                               │
  │←── SyncResult ───────────────│  (accepted[], rejected[], newSyncVersion)
  │                               │
  │  For accepted: mark outbox    │
  │    entries as confirmed        │
  │  For rejected: rollback       │
  │    local changes, notify UI   │
```

### 4.3 Server → Client (Push)

```
Client                          Server
  │                               │
  │                               │  Another client/service writes data
  │                               │  Server determines which clients
  │                               │  are authorized to see the change
  │                               │
  │←── SyncDelta ────────────────│  (changes[], serverSyncVersion)
  │                               │
  │  Apply delta to local SQLite  │
  │  Update local syncVersion     │
  │  Notify UI of changes         │
```

### 4.4 Conflict Resolution

Default strategy: **Server Wins (Last-Writer-Wins with server authority)**

```
Conflict detected when:
  - Client pushes a change for entity X at version V
  - Server has entity X at version V+N (modified by another client)

Resolution:
  1. Server rejects the client's change
  2. Server sends the current authoritative state back
  3. Client applies server state, discarding local optimistic change
  4. UI notifies user if needed (toast/notification)

Future: Configurable per-entity strategies
  - ServerWins (default)
  - ClientWins (for user-local preferences)
  - Merge (field-level merge for non-conflicting fields)
  - Custom (application-defined resolution function)
```

---

## 5. Data Model Extensions

### 5.1 Sync Metadata (Server-Side)

```csharp
// Added to base entity or as shadow properties
public interface ISyncable
{
    long SyncVersion { get; set; }       // Monotonically increasing per-entity
    DateTime LastModifiedUtc { get; set; }
    Guid LastModifiedBy { get; set; }
}
```

Server `SaveChangesAsync` interceptor automatically stamps `SyncVersion` on every write.

### 5.2 Local Sync Tables (WASM-Side)

```csharp
// Tracks pending local changes awaiting server confirmation
public class SyncOutboxEntry
{
    public Guid Id { get; set; }
    public string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public SyncOperation Operation { get; set; }  // Create, Update, Delete
    public string SerializedPayload { get; set; }
    public SyncStatus Status { get; set; }         // Pending, Confirmed, Rejected
    public DateTime CreatedAt { get; set; }
}

// Tracks the local sync state
public class SyncState
{
    public long LastServerSyncVersion { get; set; }
    public DateTime LastSyncUtc { get; set; }
    public Guid CredentialId { get; set; }
    public Guid TenantId { get; set; }
}

public enum SyncOperation { Create, Update, Delete }
public enum SyncStatus { Pending, Confirmed, Rejected }
```

---

## 6. Access Control Architecture

### 6.1 Three-Layer Model

| Layer | Where | Purpose |
|-------|-------|---------|
| **Sync boundary** | Server Sync Hub | Controls WHAT data flows to the client. Client never receives unauthorized data. |
| **Global query filters** | Both Server + WASM DbContext | Defense-in-depth. Same tenant/credential filters applied locally. |
| **Write validation** | Server Sync Hub | ALL writes validated server-side before confirmation. Client writes are optimistic only. |

### 6.2 Sync Boundary (Server-Side Authorization Gate)

The sync layer is the primary enforcement point. It determines what each client can see:

```csharp
public class SyncAuthorizationService
{
    // Determines which entities a credential is authorized to read
    public IQueryable<T> ScopeQuery<T>(IQueryable<T> query, SyncContext ctx) where T : class
    {
        // Apply tenant filter
        if (typeof(ITenantScoped).IsAssignableFrom(typeof(T)))
            query = query.Where(e => ((ITenantScoped)e).TenantId == ctx.TenantId);

        // Apply credential-level access (e.g., user can only see their own wallets)
        if (typeof(ICredentialScoped).IsAssignableFrom(typeof(T)))
            query = query.Where(e => ((ICredentialScoped)e).CredentialId == ctx.CredentialId);

        // Apply role-based access for shared entities
        // (e.g., admin can see all users in their tenant)
        return ApplyRoleBasedFilters(query, ctx);
    }

    // Validates whether a credential can perform a write operation
    public AuthorizationResult CanWrite<T>(T entity, SyncOperation op, SyncContext ctx);
}
```

### 6.3 Global Query Filters (Shared DbContext)

Applied identically on Server and WASM for consistent behavior:

```csharp
public class AppDbContext : DbContext
{
    private readonly ISessionContext _session;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Tenant isolation
        builder.Entity<Wallet>().HasQueryFilter(
            w => w.TenantId == _session.TenantId);

        // Credential scoping for user-owned entities
        builder.Entity<Wallet>().HasQueryFilter(
            w => w.CredentialId == _session.CredentialId);

        // Soft delete
        builder.Entity<Wallet>().HasQueryFilter(
            w => !w.IsDeleted);
    }
}
```

On **Server**: prevents cross-tenant/cross-user data leaks at DB level.
On **WASM**: ensures local queries behave identically, even though data is already scoped by sync.

### 6.4 Write Flow (Server Authority)

```
WASM Client:
  1. User action triggers a write (e.g., TransferWallet)
  2. LocalDbContext.SaveChangesAsync():
     a. Persist to local SQLite (optimistic)
     b. Create SyncOutboxEntry with status = Pending
     c. UI reflects the change immediately
  3. SyncEngine picks up outbox entry
  4. Sends ChangeSet to server via StreamFlow

Server:
  5. SyncAuthorizationService.CanWrite() — is this user allowed?
  6. Business validation (sufficient balance, transfer rules, etc.)
  7. If valid: apply to server DB, return Confirmed
  8. If invalid: return Rejected with reason

WASM Client:
  9a. Confirmed: mark outbox entry as Confirmed, keep local state
  9b. Rejected: rollback local SQLite change, show error to user
```

---

## 7. Registration & DI

### 7.1 Blazor Server Registration

```csharp
// In Server startup
services.AddDbContext<IAppDbContext, ServerDbContext>(options =>
    options.UseNpgsql(connectionString));

// No SyncEngine needed — direct DB access
```

### 7.2 Blazor WASM Registration

```csharp
// In WASM startup
services.AddDbContext<IAppDbContext, LocalDbContext>(options =>
    options.UseSqlite("Data Source=local.db"));

services.AddSingleton<SyncEngine>();
services.AddSingleton<SyncAuthorizationService>(); // Client-side mirror for UX

// StreamFlow connection (already exists)
services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
```

### 7.3 Conditional Registration Pattern

```csharp
public static class DataAccessInstaller
{
    public static void AddXFrameworkData(this IServiceCollection services,
        IHostEnvironment env, IConfiguration config)
    {
        if (env.IsWasmEnvironment()) // Extension method to detect WASM
        {
            services.AddDbContext<IAppDbContext, LocalDbContext>(o =>
                o.UseSqlite("Data Source=local.db"));
            services.AddSingleton<SyncEngine>();
        }
        else
        {
            services.AddDbContext<IAppDbContext, ServerDbContext>(o =>
                o.UseNpgsql(config.GetConnectionString("Default")));
        }
    }
}
```

---

## 8. Migration Path

This is a phased migration from the current architecture:

### Phase A: Foundation (Prerequisites)

1. Ensure all shared entities implement `ISyncable` (add `SyncVersion` to base entity).
2. Add `IAppDbContext` interface to the existing `AppDbContext`.
3. Verify SQLite WASM support in .NET 10 for the entity model.

### Phase B: Local DbContext (WASM)

1. Create `LocalDbContext` configured with SQLite WASM provider.
2. Implement `SyncOutboxEntry` and `SyncState` local tables.
3. Override `SaveChangesAsync` to queue writes to outbox.
4. Register `LocalDbContext` as `IAppDbContext` in WASM.

### Phase C: Sync Engine

1. Implement `SyncEngine` background service.
2. Implement `SnapshotRequest` / `SnapshotResponse` over StreamFlow.
3. Implement outbox processing (client → server push).
4. Implement server delta broadcasting (server → client push).
5. Add `SyncRequestHandler` to the StreamFlow hub.

### Phase D: Access Control Integration

1. Implement `SyncAuthorizationService` on the server.
2. Add tenant/credential scoping to snapshot queries.
3. Add write validation to push handler.
4. Apply global query filters to `LocalDbContext`.

### Phase E: Migration of Blazor Features

1. Migrate Blazor behaviors one feature at a time:
   - Replace `IIdentityServerServiceWrapper` calls with `DbContext` queries.
   - Replace `IWalletsServiceWrapper` calls with `DbContext` queries.
2. The `IServiceWrapper` interfaces remain available as fallback during migration.
3. Integration projects can be deprecated once all features use `DbContext`.

### Phase F: Advanced Features (Future)

1. Configurable per-entity conflict resolution strategies.
2. Partial sync (only sync entities the current view needs).
3. Offline support (queue writes when disconnected, sync on reconnect).
4. CRDT-based merge for collaborative editing scenarios.
5. IndexedDB as alternative/complement to SQLite for blob storage.

---

## 9. Relationship to Existing Architecture

| Component | Current Role | Future Role |
|-----------|-------------|-------------|
| **StreamFlow/SignalR** | RPC transport for service wrappers | Sync transport layer |
| **Integration projects** | Blazor client SDK (typed RPC proxies) | Deprecated once sync is active; kept as optional fallback |
| **ServiceWrapperGenerator** | Generates CRUD proxies over StreamFlow | Still useful for non-synced operations; sync handles CRUD |
| **DriverBase** | Base for SignalR RPC calls | Base for sync protocol messages |
| **DbContext (server)** | Used by server-side VSA modules | Unchanged |
| **DbContext (WASM)** | N/A (not available today) | Local SQLite replica with sync |

---

## 10. Open Questions

1. **Schema migrations in WASM:** How to handle EF migrations for the local SQLite database when the server schema evolves? Options: version-stamped schemas with auto-recreate, or WASM-side migration runner.

2. **Data volume:** For users with large datasets (thousands of transactions), should the initial snapshot be paginated/lazy-loaded? Or only sync what the current view needs?

3. **Binary serialization for sync:** Reuse MessagePack (already in StreamFlow) for sync payloads, or use a more compact delta format?

4. **Offline duration:** How long can a client be offline before a full re-sync is required vs. incremental delta?

5. **Multi-tab:** If user has multiple browser tabs open, should they share a single local SQLite, or each have independent replicas?

6. **Server-side Blazor with sync:** Should Blazor Server also optionally use the sync pattern (for caching/performance), or always direct DB?
