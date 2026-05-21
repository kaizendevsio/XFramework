---
title: "EF Core Data Access Patterns"
date: 2026-05-21
category: conventions
module: XFramework
problem_type: convention
component: data_access
severity: high
applies_when:
  - "Adding or reviewing EF Core entities, configurations, migrations, tests, or IDataContext usage in XFramework"
tags: [ef-core, data-access, migrations, datacontext, testcontainers]
---

# EF Core Data Access Patterns

## Status

Current implementation guide. Source code remains authoritative when this document and implementation disagree.

## Current Data Surfaces

- `src/Kernel/XFramework.Domain/Contexts/AppDbContext.cs` is the shared application DbContext.
- `src/Kernel/XFramework.Domain/Contexts/XDbContext.cs` provides global soft-delete, tenant filtering, and save-time validation/defaulting.
- `src/Kernel/XFramework.Domain/Interceptors/AuditInterceptor.cs` populates audit timestamps through EF Core interception.
- Module API projects register `DbContext` as `AppDbContext` from each module's `Installers/DbInstaller.cs`.
- `src/Tools/XFramework.MigrationRunner/Program.cs` applies pending migrations against PostgreSQL from `DefaultDatabaseConnection`.
- `src/Kernel/XFramework.Core/DataContext/ServerDataContext.cs` adapts EF Core to the shared `IDataContext` interface for in-process callers.
- `src/Infrastructure/XFramework.Integration/DataContext/RemoteDataContext.cs` adapts `IDataContext` to generated remote service wrappers for client-side callers.

## AppDbContext Discovery

`AppDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly` for every loaded assembly whose name contains `Domain.Shared` or `XFramework.Domain`.

This means entity configuration discovery depends on loaded assemblies, not a central list. A module contributes EF mappings only when its `Domain.Shared` assembly is loaded by the current service/test process. API module `ProjectReference` graphs and test setup determine which module assemblies are present.

Use this placement rule:

- Framework-wide shared contracts belong under `src/Shared/XFramework.Domain.Shared`; EF Core context, migrations, and framework-wide configurations belong under `src/Kernel/XFramework.Domain`.
- Module-owned entities and `IEntityTypeConfiguration<T>` classes belong in the module's `*.Domain.Shared` project.
- Module API projects should reference their `*.Domain.Shared` project so `AppDbContext` can discover configurations at runtime.
- Service code should use explicit `DbSet` properties only when they already exist; for module entities, prefer `db.Set<TEntity>()` unless a partial `AppDbContext` extension already declares a `DbSet`.

## DbContext Registration Defaults

Module `DbInstaller` classes currently register:

- `AuditInterceptor` as scoped.
- `DbContext` mapped to `AppDbContext`.
- PostgreSQL through Npgsql.
- `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)`.
- `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)`.
- `AuditInterceptor` through `AddInterceptors(...)`.

Do not assume `AppDbContext` tracks query results by default. For writes, attach/update entities deliberately or query with tracking when the change depends on EF tracking behavior.

## Global Filters And Save Behavior

`XDbContext` applies global query filters to every mapped entity that implements:

- `ISoftDeletable`: filters out rows where `IsDeleted` is true.
- `IHasTenantId`: filters to the current tenant ID.

Tenant resolution is per query through a DbContext property. It checks authenticated HTTP claims named `tenantId`, `TenantId`, or `tid`, then falls back to `Tenant:DefaultId`, then `Guid.Empty` for design-time and migration-only contexts.

`XDbContext.SaveChanges` and `SaveChangesAsync` call `OnBeforeSaveChanges` before EF saves:

- Converts deletes on `BaseModel.IsDeleted` entities into soft-delete updates.
- Sets `DeletedAt` when converting a delete.
- Defaults `IsEnabled` and `IsDeleted` when unset.
- Throws if a `BaseModel.TenantId` value is null or `Guid.Empty`.

`AuditInterceptor` only sets `CreatedAt` on added `IAuditable` entities and `ModifiedAt` on modified `IAuditable` entities. It does not set user IDs or tenant IDs.

## Query Rules

- Use projections with `.Select(...)` for read DTOs.
- Keep filters on `IQueryable<T>` until execution; do not call `.ToList()` before applying filters.
- Use `.IgnoreQueryFilters()` only for explicit admin/system behavior that needs deleted or cross-tenant rows.
- Include tenant IDs in cache keys for any cached multi-tenant read result.
- Use `ExecuteUpdateAsync` and `ExecuteDeleteAsync` for bulk operations when they fit the business rule.
- Do not call `SaveChangesAsync` inside loops; batch changes and save once.
- Use `.AsTracking()` or an explicit attach/update pattern when modifying entities loaded from a no-tracking query.

## Migrations

The migration runner is the deployment-time authority for applying EF migrations. It:

- Requires `DefaultDatabaseConnection`.
- Builds `DbContextOptions<AppDbContext>` with Npgsql.
- Enables Npgsql retry-on-failure.
- Ignores EF's pending model changes warning.
- Lists and applies pending migrations through `context.Database.Migrate()`.

When adding migrations, make sure the migration sees the same module assemblies that production services load. If a module configuration is missing from a migration, verify the relevant `Domain.Shared` assembly is referenced and loaded before changing `AppDbContext` discovery.

Do not add runtime database migration calls to service startup to compensate for missing migrations. `XApplication` treats migrations as a migration-runner/init-container responsibility.

## Tests

Integration tests and benchmarks use Testcontainers PostgreSQL in the current test surface. Representative paths include:

- `src/Tests/IdentityServer.IntegrationTests/Infrastructure/IntegrationTestFixture.cs`
- `src/Tests/Wallets.IntegrationTests/Infrastructure/WalletsTestFixture.cs`
- `src/Tests/XFramework.TestInfrastructure/TestHelpers.cs`

Tests that need module mappings may need to force-load module `Domain.Shared` assemblies before constructing `AppDbContext`, because discovery scans loaded assemblies. Prefer PostgreSQL-backed tests for EF behavior that depends on Npgsql translation, PostgreSQL extensions, migrations, or query filters.

## IDataContext: Local And Remote

`IDataContext` is the shared abstraction for query and persistence flows that must work both in-process and remotely.

Use `ServerDataContext<TDbContext>` when the caller runs in the same process as EF Core. It delegates `Query<T>()`, `Add`, `Update`, `Remove`, and `SaveChangesAsync` directly to the injected DbContext and returns `DataContextResult` for save failures.

Use `RemoteDataContext` when the caller runs outside the owning service process. It:

- Builds `RemoteQuery<T>` instances for query descriptors.
- Buffers `Add`, `Update`, and `Remove` changes until `SaveChangesAsync`.
- Uses source-generated `DataContextEntityRegistrations.GetDataContextServiceWrapperMap()` to map entity type names to service wrapper type names.
- Requires all pending changes in one `SaveChangesAsync` call to target one owning service.
- Resolves the generated `IDataContextServiceWrapper` through DI and sends a `SaveChangesRequest` serialized with MemoryPack.
- Uses generated change trackers when available to send update patches instead of whole entities.

Do not treat remote `IDataContext` as direct EF Core. It is a remote query/change transport over generated wrappers, and cross-service writes must be split into separate data-context scopes.

## Related Guidance

- `docs/solutions/architecture-patterns/decentralized-remote-data-context.md` for remote data-context architecture.
- `docs/solutions/developer-experience/blazor-idatacontext-migration.md` for migrating Blazor wrapper CRUD calls to `IDataContext`.
- `docs/solutions/best-practices/xframework-caching-strategy.md` for cache-key and invalidation rules around EF reads.
- `docs/solutions/conventions/xframework-best-practices.md` for broader VSA and service conventions.
