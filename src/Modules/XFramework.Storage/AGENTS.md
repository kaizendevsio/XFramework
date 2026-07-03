# Storage Module Agent Instructions

This file applies only to `src/Modules/XFramework.Storage/**`. Root `AGENTS.md`, `CLAUDE.md`, `docs/solutions/conventions/xframework-best-practices.md`, and `rules/BackendGuidelines.md` still apply.

## Module Purpose

`XFramework.Storage` owns tenant file metadata, tenant buckets, resumable uploads, provider routing, signed/private URLs, public/CDN URLs, soft delete, restore, and retention cleanup.

Storage is a standalone service, but it uses the shared XFramework PostgreSQL database with the `Storage` schema. Do not split it into a separate database without an approved architecture decision.

Projects:

- `Storage.Api` - VSA endpoints, `StorageService`, provider implementations, health, feature gate, and Bolt handler registration.
- `Storage.Domain.Shared` - Storage entities, EF configurations, request/response contracts, enums, and generated endpoint metadata.
- `Storage.Integration` - generated `IStorageServiceWrapper` and wrapper registration for other modules.

Historical compatibility note: moved `StorageFile*` entity namespaces currently remain `XFramework.Domain.Shared.Contracts`. Do not rename those namespaces casually; doing so can break EF migrations, remote data-context routing, and cross-module references.

## Integration Rules

- Use `IStorageServiceWrapper` for cross-module Storage behavior. Communications, Community, Portal, Blazor, and future modules should call the wrapper for validation, upload sessions, upload parts, completion, URLs, delete, restore, and retention operations.
- Do not query or write Storage tables directly from another module to validate file references. Use `ValidateStorageFileReference` through the wrapper.
- Direct `IDataContext.Query<StorageFile>()` is acceptable only for intentional read-only metadata display paths. Do not use remote data context to bypass Storage workflow rules.
- Do not add client-provided `TenantId` fields to Storage request contracts. Storage derives tenant identity from authenticated context or trusted signed request metadata.
- Protected Storage HTTP endpoints must stay behind authorization and `TenantModuleFeatureKeys.Storage`.

## Upload Contract

- Bolt is the primary module-to-module path. Upload parts are binary `byte[]` chunks over MemoryPack request contracts.
- Do not send file content as JSON base64 or JSON byte arrays for normal module integrations.
- The manual REST upload-part endpoint exists only for external/manual binary upload cases where generated binding is insufficient. It must remain `application/octet-stream`.
- Part upload retries must remain idempotent when the part number, offset, length, and SHA-256 match. Mismatched retries must return conflict.
- Require and validate SHA-256 for uploaded parts. Completion must validate expected final hash when supplied.
- Keep provider limits explicit. S3-compatible production uploads must respect S3 multipart limits unless a test fixture explicitly disables provider-limit enforcement.

## Provider Rules

- V1 supports Azure Blob and S3-compatible storage. MinIO is configured through the S3-compatible provider.
- Do not add provider-specific behavior directly into endpoints. Use `IStorageObjectProvider` and `IStorageProviderFactory`.
- New provider settings belong in `StorageOptions`, provider profiles, EF configuration, appsettings, compose/env docs, and tests.
- Do not persist raw provider secrets into new metadata rows. Prefer configuration or secret reference fields.
- `BoltConfiguration:ClientName` for the Storage service must remain `Storage`; generated wrappers route to the SHA-256 of that name.
- Keep `XFrameworkServiceManifest` current so Operations Dashboard and tenant-module discovery show Storage correctly.

## Metadata, Delete, And Retention

- Storage metadata must stay tenant-scoped. Physical buckets are tenant-specific and deterministic.
- Private is the default visibility. Private files return signed URLs. Public files require configured public/CDN base URLs.
- Delete is metadata-first soft delete. Physical object deletion happens through retention cleanup.
- Restore must work only before physical deletion. After `ObjectDeletedAt` is set, do not silently restore metadata.
- Retention cleanup must be idempotent and must not repeatedly delete objects already marked physically deleted.

## Database And Migration Rules

- Keep Storage EF configurations in `Storage.Domain.Shared/Configurations`.
- Preserve the `Storage` schema and existing Storage table names unless an explicit migration plan says otherwise.
- Review migrations for destructive drop/recreate of existing Storage tables before committing.
- Make sure the migration runner continues to reference/load `Storage.Domain.Shared`.
- Index tenant-scoped query paths by `TenantId` first where practical.

## Docker, CI, And Discovery

- `docker-compose.yml` must keep the `storage` service dependent on `bolt-hub`, `postgres`, and `minio`.
- MinIO uses the published `minio/minio` image; there is no local MinIO Dockerfile.
- Storage should use the repo generic `Dockerfile` with `PROJECT_PATH=src/Modules/XFramework.Storage/Storage.Api/Storage.Api.csproj`.
- If Storage endpoints, providers, ports, or health behavior change, update compose, `.env.example`, and xeon-dev workflows in the same PR.
- Portal navigation should expose Storage only when `TenantModuleFeatureKeys.Storage` is enabled.

## Testing Expectations

- Do not add SQLite-based Storage tests. Storage behavior depends on PostgreSQL, migrations, tenant filters, indexes, and provider/session persistence.
- Put Storage integration tests under `src/Tests/Storage.IntegrationTests`.
- Follow the existing PostgreSQL/Testcontainers integration pattern. Tests may skip only when Docker/Testcontainers is unavailable.
- Cover wrapper/Bolt flows, REST binary upload binding, tenant isolation, part idempotency/conflict, completion hash validation, signed/private URLs, public/CDN URLs, soft delete, restore, and retention cleanup.
- When changing Communications or Community file integration, update their tests to prove they call `IStorageServiceWrapper` and reject missing, deleted, or wrong-tenant files.

Useful verification commands:

```powershell
dotnet build XFramework.slnx --no-restore
dotnet test src\Tests\Storage.IntegrationTests\Storage.IntegrationTests.csproj --no-build
docker-compose config
```

## Maintenance Rule

When fixing a bug or changing any Storage feature, contract, provider behavior, endpoint, wrapper flow, tenant/security behavior, Docker/CI wiring, or testing pattern, update this `AGENTS.md` in the same PR if the change affects how future agents should work with this module.
