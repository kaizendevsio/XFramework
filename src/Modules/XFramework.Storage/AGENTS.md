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
- Generated `StorageFile` and `StorageFileType` reads require `StorageAuthorizationCapabilities.View` (`storage:view`). Workflow reads require `storage:view`; workflow mutations require `storage:manage`. Do not replace these actor capabilities with client-trusted tenant input or a service-only bypass.
- Keep generated entity services registered with `GeneratedEntityServiceRegistrations` and generated entity REST routes mapped with `GeneratedEntityEndpointRoutes`. Mapping routes without their generated services causes runtime 500 responses.

## Upload Contract

- Bolt is the primary module-to-module path. Upload parts are binary `byte[]` chunks over MemoryPack request contracts.
- Use `EnsureStorageUploadMetadata` through `IStorageServiceWrapper` when another module needs tenant-scoped file type and identifier metadata. The operation owns creation and must remain idempotent for repeated tenant/name inputs.
- Do not send file content as JSON base64 or JSON byte arrays for normal module integrations.
- The manual REST upload-part endpoint exists only for external/manual binary upload cases where generated binding is insufficient. It must remain `application/octet-stream`.
- Part upload retries must remain idempotent when the part number, offset, length, and SHA-256 match. Mismatched retries must return conflict.
- Part reservation, completion, and abort use the same tenant/session advisory-lock namespace plus persisted lease statuses. Provider calls stay outside database transactions; preserve compare-and-set finalization and return conflict when ownership changes.
- Persist the file/session metadata before initiating provider multipart state. S3 abort recovery must keep supporting exact object-key lookup when an upload ID could not be persisted.
- Require and validate SHA-256 for uploaded parts. Completion must validate expected final hash when supplied.
- Single-part completion verifies from the persisted part hash. Multipart completion moves the file through `Verifying`/`VerificationInProgress`; background maintenance streams the provider object and marks it `Available` only after size/hash verification.
- Uploads created with `RequireClaim` receive a Storage-owned `UnclaimedUntil` deadline on completion. The owning module must call `ClaimStorageFile` through `IStorageServiceWrapper`; claiming is tenant-scoped and idempotent.
- Keep provider limits explicit. S3-compatible production uploads must respect S3 multipart limits unless a test fixture explicitly disables provider-limit enforcement.

## Provider Rules

- V1 supports Azure Blob and S3-compatible storage. MinIO is configured through the S3-compatible provider.
- Public and private files use separate tenant buckets identified by `StorageBucketPurpose`. Do not mix private objects into a provider-managed public bucket.
- `ProviderManaged` public delivery configures Azure container access or an S3 bucket read policy. `PrivateOriginCdn` keeps the provider private and emits only the configured CDN URL. `Disabled` rejects public uploads.
- Ensuring a private or private-origin S3 bucket must remove anonymous bucket policy. Legacy public URL fields are cleared during migration so existing mixed-visibility objects fail closed to signed downloads.
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
- A file in `Deleting` state cannot be restored. Maintenance must not abort an expired session while it has a fresh `Uploading` part lease.
- Retention cleanup must be idempotent and must not repeatedly delete objects already marked physically deleted.
- Missing provider profile/bucket metadata must leave deletion rows retryable. Set `ObjectDeletedAt` only after provider-confirmed deletion.
- Storage background maintenance owns expired incomplete-session aborts and physical deletion of expired unclaimed files. Failed provider operations remain due for the next bounded poll; existing files with a null `UnclaimedUntil` are unaffected.
- Maintenance work uses exclusive persisted leases (`Aborting`, `Deleting`, and `VerificationInProgress`). Stale leases may be reclaimed after `MaintenanceLeaseSeconds`; do not perform provider work before a successful claim.

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
- Storage readiness must validate the selected object provider and its configured readiness bucket/container. Compose health uses `/health/ready` and waits for `minio-init` to create the MinIO readiness bucket.
- Pass `hostEnvironment` to `AddXFrameworkBoltClient` so non-Development startup validates secure `wss://` transport configuration. Do not bypass that validation with the environment-free overload or a plaintext client URL.
- If Storage endpoints, providers, ports, or health behavior change, update compose, `.env.example`, and xeon-dev workflows in the same PR.
- Portal navigation should expose Storage only when `TenantModuleFeatureKeys.Storage` is enabled.

## Testing Expectations

- Do not add SQLite-based Storage tests. Storage behavior depends on PostgreSQL, migrations, tenant filters, indexes, and provider/session persistence.
- Put Storage integration tests under `src/Tests/Storage.IntegrationTests`.
- Follow the existing PostgreSQL/Testcontainers integration pattern. CI must supply PostgreSQL, MinIO, and Azurite so runtime and provider-contract tests execute rather than skip.
- Keep metadata/completeness/validator contract tests outside the assembly-wide Testcontainers setup namespace so they remain executable without Docker.
- Cover wrapper/Bolt flows, REST binary upload binding, tenant isolation, part idempotency/conflict, completion hash validation, signed/private URLs, public/CDN URLs, soft delete, restore, and retention cleanup.
- Cover generated REST/wrapper/remote-`IDataContext` authorization parity, including missing or incorrect capability, service-only denial, explicit wrong-tenant targeting, and disabled Storage feature behavior.
- When changing Communications or Community file integration, update their tests to prove they call `IStorageServiceWrapper` and reject missing, deleted, or wrong-tenant files.

Useful verification commands:

```powershell
dotnet build XFramework.slnx --no-restore
dotnet test src\Tests\Storage.IntegrationTests\Storage.IntegrationTests.csproj --no-build
docker-compose config
```

## Maintenance Rule

When fixing a bug or changing any Storage feature, contract, provider behavior, endpoint, wrapper flow, tenant/security behavior, Docker/CI wiring, or testing pattern, update this `AGENTS.md` in the same PR if the change affects how future agents should work with this module.
