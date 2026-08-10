---
title: "Storage Backend Audit"
date: 2026-08-11
category: workflow-issues
module: Storage
problem_type: security_and_backend_compliance
component: storage
severity: none
status: resolved
tags: [storage, security, bolt, service-identity, ef-core, providers, testing]
---

# Storage Backend Audit

## Audit Scope

- Baseline: `origin/develop` at `e3d91743a2789e5e689fa86f7bc6f5852285245b`, including PR #403 generated-authorization parity.
- Implementation branch: `codex/storage-production-hardening`.
- Authority: `CLAUDE.md`, `rules/BackendGuidelines.md`, and their canonical solution documents.
- Method: fresh post-PR #403 review using the `xframework-audit-module` workflow, followed by implementation, PostgreSQL/provider verification, and an independent diff review.
- Areas: VSA, generated/manual endpoints, services, EF Core and schema ownership, validation, caching, packages, wrappers, remote `IDataContext`, Bolt authorization, actor/effective-tenant enforcement, service-token reuse, providers, maintenance, migrations, Docker, and CI.

## Executive Result

All findings from the current audit are resolved in the implementation branch.

- Critical: 0 open.
- High: 0 open.
- Medium: 0 open.
- Low: 0 open.

The independent final review identified restore/delete and maintenance/upload races plus a legacy public-bucket upgrade risk. Those were addressed before this report was closed.

## Current Findings

### Critical

No open Critical findings.

### High

No open High findings.

### Medium

No open Medium findings.

### Low

No open Low findings.

## Resolved High Findings

### H1. Generated entity authorization metadata and parity

**Resolution:** `StorageFile` and `StorageFileType` now declare the canonical `storage` feature, `view` capability, actor-required read access, and disabled generated caching at `src/Modules/XFramework.Storage/Storage.Domain.Shared/Contracts/StorageFile.cs:6-13` and `StorageFileType.cs:6-13`. Generated services and REST routes are registered at `src/Modules/XFramework.Storage/Storage.Api/Installers/ServicesInstaller.cs:17-20` and `Storage.Api/Program.cs:37-39`.

**Impact removed:** same-tenant actors without `storage:view`, service-only callers, wrong-tenant actors, and tenants with Storage disabled no longer receive generated Storage metadata.

**Coverage:** policy completeness is enforced at `src/Tests/Storage.IntegrationTests/Tests/GeneratedEntityAuthorizationCompletenessTests.cs:19`; generated REST, wrapper, remote `IDataContext`, capability, tenant, service-only, and feature-denial parity is exercised at `StoragePostgresTests.cs:256`.

### H2. REST and Bolt upload authorization consistency

**Resolution:** generated REST endpoints explicitly require actor capabilities and clear service-token requirements while Bolt handlers retain `storage.read`/`storage.write`. Session creation is representative at `src/Modules/XFramework.Storage/Storage.Api/Features/Sessions/Create/Endpoint.cs:8-13`; the manual binary endpoint requires `storage:manage` before buffering at `Sessions/UploadPart/Endpoint.cs:48-70`.

**Impact removed:** actor HTTP clients can create, upload, and complete sessions without an edge-service token, while Bolt module calls still require destination service scopes.

**Coverage:** all 14 generated REST policies are guarded at `src/Tests/Storage.IntegrationTests/Tests/StorageModuleContractTests.cs:23`; actor-only success, missing capability, and disabled-feature denial are tested at `StoragePostgresTests.cs:78`; binary authorization-before-buffering remains covered at `StorageBoltScopeContractTests.cs:57`.

### H3. Public delivery is provider-backed and upgrade-safe

**Resolution:** public/private objects use purpose-specific buckets (`StorageTenantBucket.cs:28`, `StorageTenantBucketConfiguration.cs:31`). Azure enforces container access at `src/Modules/XFramework.Storage/Storage.Api/Services/Providers/AzureBlobStorageProvider.cs:22-34`; S3 removes anonymous policy for private/CDN buckets and installs it only for provider-managed public buckets at `S3CompatibleStorageProvider.cs:319-354`. Public URLs are persisted only after successful completion at `StorageService.cs:615-638`.

The migration defaults existing buckets to private and clears legacy fabricated public/CDN URLs so old objects fall back to signed download at `src/Kernel/XFramework.Domain/Migrations/20260810144201_StorageProductionHardening.cs:26-39`. Re-ensuring a private S3 bucket removes a legacy public policy.

**Impact removed:** Storage no longer returns unsigned URLs for inaccessible objects or places new private objects into an anonymously readable managed bucket.

**Coverage:** PostgreSQL bucket separation and bounded URL behavior is at `StoragePostgresTests.cs:570`; migration operations are guarded at `StorageModuleContractTests.cs:53`; real MinIO policy transition/private/public/download behavior is at `StorageMinioProviderTests.cs:22`; Azurite public/private/SAS behavior is at `StorageAzuriteProviderTests.cs:24`.

### H4. Multipart transitions and external side effects are concurrency-safe

**Resolution:** file/session metadata is durable before multipart initiation at `StorageService.cs:238-261`, enabling cleanup even if the upload ID cannot be persisted. S3 can recover an already-completed upload and can locate incomplete uploads by exact object key when the upload ID is absent at `S3CompatibleStorageProvider.cs:113-130` and `:185-229`.

Part reservation precedes provider writes and uses persisted `Uploading` ownership plus compare-and-set finalization at `StorageService.cs:296-452`. Upload, complete, and abort share the same tenant/session advisory-lock namespace at `:309-311`, `:518-520`, and `:676-678`. Completion/abort use exclusive states and stale leases. Maintenance excludes sessions with a fresh uploading-part lease at `StorageMaintenanceService.cs:30-36` and `:91-97`.

Restore now rejects a claimed physical deletion and maps concurrency loss to conflict at `StorageService.cs:1026-1048`, preventing a successful restore response after provider deletion begins.

**Impact removed:** conflicting retries cannot overwrite winning part metadata; completion and abort cannot both win; maintenance cannot abort a live part upload; restore cannot resurrect metadata for an object being deleted; provider-commit retries are recoverable.

**Coverage:** parallel part/complete retries at `StoragePostgresTests.cs:335`, complete-versus-abort at `:387`, maintenance-versus-upload at `:449`, cleanup-versus-restore at `:491`, and real MinIO repeat-complete/upload-ID-less abort at `StorageMinioProviderTests.cs:22`.

### H5. Missing provider metadata remains retryable

**Resolution:** retention cleanup claims deletion work, but missing/disabled profile or bucket metadata releases the lease without setting `ObjectDeletedAt`; provider-confirmed deletion alone finalizes physical deletion at `StorageService.cs:1100-1150`. Background cleanup follows the same retryable rule at `StorageMaintenanceService.cs:172-198`.

**Impact removed:** metadata inconsistencies no longer create permanent provider orphans while falsely recording physical deletion.

**Coverage:** metadata removal, repair, and later successful deletion is tested at `StoragePostgresTests.cs:526`.

### H6. Signed URL lifetime is bounded

**Resolution:** `MaxSignedUrlExpirationMinutes` defaults to 60 and is enforced in validation and service defense-in-depth at `StorageRequestValidators.cs:90-97` and `StorageService.cs:917-924`.

**Impact removed:** authorized callers cannot mint effectively permanent private-object URLs.

**Coverage:** lower/upper rejection is at `StorageRequestValidatorTests.cs:73-88`; provider contracts verify usable bounded SAS/presigned URLs.

### H7. Storage tests execute in CI

**Resolution:** Bolt CI starts pinned MinIO and Azurite services and runs the complete Storage suite against PostgreSQL at `.github/workflows/bolt-phase0-ci.yml:185-251`. Publish CI runs Docker-independent authorization, migration, readiness, and validator guards at `.github/workflows/publish.yml:44-49`.

**Impact removed:** Storage authorization/provider regressions can no longer merge solely because local Testcontainers infrastructure was unavailable.

**Coverage:** final Release execution passed 35/35 with no skips against PostgreSQL, MinIO, and Azurite.

## Resolved Medium Findings

### M1. Large multipart completion no longer re-downloads in the Bolt request

Multipart completion returns `202` with `Verifying`; maintenance claims `VerificationInProgress`, streams and hashes the provider object, validates length/hash, then marks it available or failed at `StorageService.cs:609-654` and `StorageMaintenanceService.cs:205-310`. Single-part completion uses the already verified part hash.

Coverage includes multipart asynchronous verification and maintenance retry behavior in `StoragePostgresTests.cs:140-183` and `:676-734`.

### M2. Maintenance work is exclusive across replicas

Expired-session aborts, unclaimed-file deletion, and verification use compare-and-set lease states and concurrency tokens at `StorageMaintenanceService.cs:80-146`, `:149-198`, and `:205-310`. Fresh uploading parts exclude maintenance claims.

Coverage races two maintenance instances at `StoragePostgresTests.cs:425` and verifies active-upload exclusion at `:449`.

### M3. Readiness checks the configured object backend

`StorageProviderReadinessHealthCheck` resolves the configured default provider with a bounded timeout at `src/Modules/XFramework.Storage/Storage.Api/Health/StorageProviderReadinessHealthCheck.cs:7-34`. Provider registrations are singleton/stateless at `Storage.Api/Installers/ServicesInstaller.cs:24-30`. Compose waits for `minio-init` and probes `/health/ready` at `docker-compose.yml:89-105` and `:448-455`.

Coverage includes healthy/unhealthy health-check results at `StorageModuleContractTests.cs:82`, real MinIO readiness at `StorageMinioProviderTests.cs:22`, and Azurite readiness at `StorageAzuriteProviderTests.cs:24`.

### M4. Request validation is complete and bounded

Every Storage request has a FluentValidation validator in `src/Modules/XFramework.Storage/Storage.Api/Validation/StorageRequestValidators.cs:14-128`. Limits cover IDs, file/content/profile lengths, enums, size/part bounds, SHA-256 format, page size, signed URL expiry, and null chunk payloads.

Coverage is at `StorageRequestValidatorTests.cs:23-94`; malformed null payloads return validation failures rather than server exceptions.

## Resolved Low Findings

### L1. Service access tokens are reused without duplicate acquisitions

The trusted target initializer stores the exact validated opaque token, audience, client, and normalized scopes at `src/Infrastructure/XFramework.Integration/Security/TrustedServiceTargetContextInitializer.cs:25-60`. `BoltDriver` reuses it only when trusted invocation identity, audience, client, and scopes match at `src/Infrastructure/XFramework.Integration/Drivers/BoltDriver.cs:387-412`; otherwise it falls back to the normal cached provider.

Coverage is at `src/Tests/XFramework.Core.Tests/Security/TrustedServiceTargetContextInitializerTests.cs:18-184` and `src/Tests/Bolt.Tests/BoltDriverIntegrationTests.cs:82-132`.

### L2. Inactive cache declarations were removed

`StorageFile` and `StorageFileType` explicitly set generated cache duration to zero at `StorageFile.cs:13` and `StorageFileType.cs:13`. Storage does not imply cache behavior without a cross-client invalidation contract.

Coverage is part of the generated authorization completeness guard at `GeneratedEntityAuthorizationCompletenessTests.cs:19-51`.

## PR #403 Revalidation

PR #403 remains intact. Storage uses centralized generated authorization metadata and does not add module-local token validation, client-trusted tenant authority, generic unauthorized remote mutation, or coarse service-only bypasses.

- Generated REST, generated entity services, wrappers, and remote `IDataContext` use the same `storage:view` policy.
- Actor identity and service identity remain distinct.
- Effective tenant comes from trusted invocation context.
- Claim/Delete keep their Bolt service-target caller/scope restrictions while actor-facing REST remains actor-tenant scoped.
- Generic remote mutation remains disabled; Storage workflows use `IStorageServiceWrapper`.

## No-Longer-Applicable Findings

1. Client-selected tenant authority remains resolved; Storage uses trusted effective tenant context.
2. Unauthenticated Bolt handlers remain resolved; all 15 handlers declare `storage.read` or `storage.write` scopes.
3. Service-versus-actor confusion remains resolved by centralized invocation authorization.
4. Direct cross-module Storage table writes remain prohibited; workflow integration uses `IStorageServiceWrapper`.
5. SQLite concerns remain inapplicable to Storage; its runtime suite uses PostgreSQL/Testcontainers.
6. Generated authorization findings predating PR #403 are stale; current policy completeness and parity are explicitly tested.

## Verification Evidence

- `dotnet build src/Modules/XFramework.Storage/Storage.Api/Storage.Api.csproj -c Release --no-restore`: passed, 0 warnings.
- `dotnet build src/Tests/Storage.IntegrationTests/Storage.IntegrationTests.csproj -c Release --no-restore`: passed, 0 warnings.
- Storage Release suite against isolated PostgreSQL plus real MinIO and Azurite: 35 passed, 0 failed, 0 skipped.
- `TrustedServiceTargetContextInitializerTests`: 5 passed.
- `BoltDriverIntegrationTests`: 7 passed.
- `dotnet ef migrations has-pending-model-changes --context AppDbContext ...`: no pending model changes.
- `docker-compose --env-file .env.example config --quiet`: passed.
- Storage API and test projects have no known vulnerable NuGet packages from configured sources.
- Full `dotnet build XFramework.slnx -c Release --no-restore` remains blocked by the pre-existing missing `Moq` reference in `src/Tests/POS.IntegrationTests/PosBoltRuntimeIntegrationTests.cs:11`; Storage projects built successfully before that unrelated solution error.

## Residual Risks

- Provider contracts use MinIO and Azurite. A production AWS account and production Azure account smoke test is still required before changing credentials, public-access controls, block-public-access policy, or CDN origin configuration in a target environment.
- Readiness intentionally represents the configured default backend. A tenant-specific provider-profile override can fail independently and is reported on that tenant's request path rather than making the entire Storage service globally unready.
- Malware scanning remains outside V1 scope; quarantine-compatible statuses exist but no scanner is integrated.
