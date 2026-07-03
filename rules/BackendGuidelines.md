# XFramework Backend Guidelines

This file is mandatory context before changing backend services, API modules, EF Core models/configurations, migrations, service wrappers, caching, or database/runtime configuration.

## Architecture Decision

XFramework uses a single physical PostgreSQL database with schema-per-module separation.

- The shared database is intentional. Do not introduce database-per-service as a default architecture.
- Module schemas are ownership boundaries, not an invitation for arbitrary cross-module mutation.
- Services remain independently deployed API/runtime units, but persistence is coordinated through one PostgreSQL database and one migration path.
- The standard target shape is: single database, module-owned schemas, service-owned writes, wrapper/Bolt/API boundaries for business behavior.

## Required Backend Context

Before changing backend code, read the most specific docs for the surface:

- `CLAUDE.md`
- `docs/solutions/conventions/xframework-best-practices.md`
- `docs/solutions/conventions/ef-core-data-access-patterns.md`
- `docs/solutions/best-practices/xframework-caching-strategy.md`
- `docs/solutions/architecture-patterns/decentralized-remote-data-context.md`
- `docs/solutions/developer-experience/portal-service-wrapper-and-integration-test-contract.md`

Current source code still wins if a document conflicts with implementation.

## Module And Schema Ownership

- Each module owns its schema and the tables/configurations inside it, such as `Identity`, `Wallet`, `Communications`, `Inventario`, `Notifications`, and `Bolt`.
- A service may read another module's data only through an approved query path, wrapper, or deliberate read model.
- A service must not write another module's schema directly unless an existing documented contract explicitly allows it.
- Business workflows must go through the owning module's endpoint/service wrapper when validation, tenant derivation, feature gates, idempotency, ledger posting, allocation, audit, or status transitions are involved.
- Cross-module references should usually store the referenced module's ID. Add cross-schema foreign keys only when the lifecycle is stable and intentionally coupled.

## Migrations And Database Shape

- Use the migration runner/init-container path as the deployment authority for schema changes.
- Do not add runtime migration calls in individual services to compensate for missing migrations.
- Keep module EF configurations in the module's `*.Domain.Shared` project.
- Make sure the migration runner references and loads the module assemblies needed for the model.
- Avoid broad migrations that touch unrelated module schemas.
- Review indexes, constraints, default values, and tenant/soft-delete behavior as part of every schema change.

## EF Core Rules

- Keep `AppDbContext` as the shared application DbContext unless there is an explicit architecture change.
- Use projections for read endpoints and UI lists. Do not load full entity graphs when DTOs are enough.
- Paginate every list/report endpoint. No unbounded `.ToListAsync()` on user-facing paths.
- Keep filters on `IQueryable<T>` until database execution.
- Do not call `SaveChangesAsync` inside loops. Batch changes and save once.
- Use `AsTracking()` only when the service is intentionally modifying loaded entities.
- For read paths, rely on no-tracking defaults and add explicit `AsNoTracking()` on hot or unclear paths.
- Prefer `ExecuteUpdateAsync` and `ExecuteDeleteAsync` for bulk operations when they match the business rule.
- Prefer projections over large `Include()` chains. If multiple includes are required, verify split-query behavior and result size.
- Use `IgnoreQueryFilters()` only for explicit admin/system behavior and document why tenant or soft-delete filters are bypassed.
- Include the `CancellationToken` in every async database operation.

## Performance And Scale Rules

- Index for the actual query shape, especially `TenantId`, status, dates, foreign keys, idempotency keys, and lookup codes.
- For multi-tenant tables, hot indexes should usually begin with `TenantId` when tenant-scoped queries dominate.
- Keep high-volume tables such as messages, sessions, audit logs, wallet ledger/outbox, notifications, and inventory movements on a monitoring list.
- Consider partitioning, retention, archiving, materialized views, read models, or replicas for proven hot paths. Do not pre-split databases without evidence.
- Watch total PostgreSQL connections across all services. Per-service pool sizes must fit the database, not only the individual service.
- Avoid extreme connection pool settings. If many service instances need high concurrency, evaluate PgBouncer or database capacity first.
- Heavy reporting must not run broad analytical joins on hot OLTP paths. Use cached summaries, read models, materialized views, or replicas.

## Caching Rules

- Cache hot reads, reference data, and expensive computations when invalidation is simple enough to reason about.
- Use structured cache keys: `{module}:{entity}:{identifier}` or `{module}:tenant:{tenantId}:{entity}:{identifier}`.
- Include tenant identity in every cache key for tenant-specific data.
- On writes, invalidate both exact item keys and affected list/query prefixes.
- Always set TTLs. Do not create indefinite application cache entries.
- Cache failures must degrade gracefully and must not fail the business operation.
- Do not cache large mutable object graphs or sensitive data unless the cache boundary is explicitly safe.

## Transactions, Events, And Consistency

- Use single-database transactions for local module invariants when they are appropriate.
- Do not rely on cross-schema transactions as the default integration mechanism between modules.
- Use outbox/inbox or equivalent event patterns for cross-module side effects and asynchronous workflows.
- Make externally retried operations idempotent. Use unique constraints on tenant + idempotency key where applicable.
- For financial, inventory, ledger, and status workflows, prefer append-only records and explicit status transitions over destructive updates.
- Add concurrency tokens or domain-specific conflict checks for records that can be updated by parallel workflows.

## Service Boundary Rules

- Backend services own business rules. Endpoints should validate and delegate, not contain business logic.
- Portal and client surfaces must use service wrappers for business operations.
- Direct `IDataContext` mutation is only for explicitly allowlisted simple CRUD paths with integration coverage.
- Do not bypass validators, feature gates, tenant checks, idempotency, derived data updates, or ledger/allocation logic with direct data access.
- Cross-module service calls should use the existing wrapper/Bolt/API conventions rather than taking a direct dependency on another module's service internals.

## Security And Tenant Isolation

- Never trust client-provided tenant IDs for protected operations.
- Tenant-owned entities must have valid `TenantId` values before save.
- Do not bypass tenant filters in user-facing code.
- Do not log secrets, tokens, passwords, connection strings, or sensitive personal data.
- Prefer database constraints for critical uniqueness and integrity rules, not only service-level checks.
- Consider schema-specific database roles for stronger ownership boundaries when operationally ready.

## Testing And Verification

- Add or update tests when backend behavior changes.
- Use PostgreSQL-backed tests for behavior that depends on Npgsql translation, migrations, constraints, indexes, query filters, or transactions.
- Test service wrappers for business workflows.
- Test remote `IDataContext` only for paths that intentionally expose remote query or mutation.
- For schema changes, verify migration generation and model snapshot changes are scoped to the intended module.
- For performance-sensitive changes, inspect generated SQL or add focused integration coverage for query shape.

## Do

- Do keep the single-database, schema-per-module decision intact.
- Do preserve module ownership even inside the shared database.
- Do route cross-module business behavior through wrappers, Bolt, or API contracts.
- Do design indexes from actual query patterns.
- Do include tenant IDs in queries, indexes, and cache keys where tenant-scoped data is involved.
- Do batch writes and save once per unit of work.
- Do use outbox/event patterns for cross-module side effects.
- Do monitor slow queries, lock waits, connection usage, table/index bloat, cache hit rates, and p95/p99 endpoint latency.
- Do document intentional exceptions in the change that introduces them.

## Do Not

- Do not split a module into its own database without a specific approved architecture decision.
- Do not add direct writes into another module's schema from a non-owning service.
- Do not use shared database access to bypass service wrappers or endpoint validation.
- Do not add broad cross-schema foreign key chains for convenience.
- Do not run migrations from every service at startup.
- Do not add unbounded list/report queries.
- Do not load full entity graphs for read DTOs.
- Do not call `SaveChangesAsync` in a loop.
- Do not use `IgnoreQueryFilters()` without an explicit admin/system reason.
- Do not cache tenant-specific data without tenant-specific keys.
- Do not hide performance problems with cache-only fixes when the underlying query is unbounded or unindexed.
- Do not increase connection pool sizes as a first response to slow queries.
