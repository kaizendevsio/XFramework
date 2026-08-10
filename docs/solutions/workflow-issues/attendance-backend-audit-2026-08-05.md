# Attendance Backend Audit - Remediated 2026-08-10

**Baseline:** `origin/develop` at `e3d91743a2789e5e689fa86f7bc6f5852285245b`, including PR #403 (`eef41054`, generated entity authorization parity), plus the scoped remediation on `codex/attendance-audit-core-correctness`

**Scope:** `XFramework.Attendance`, its IdentityServer dependency, Portal and wrapper boundaries, PostgreSQL integration suite, generated authorization, trusted invocation, Bolt, token caching, schema ownership, deployment configuration, and CI

**Overall grade:** A-

## Executive Summary

The complete post-IdentityServer and post-PR #403 audit was rerun against the remediated code. No Critical, High, Medium, or Low findings remain open in the scoped review.

Attendance now uses explicit actor-authorized read wrappers while generic remote `IDataContext` remains fail-closed. Actor attribution comes only from trusted invocation context, participant history is date-effective, credentials are verified through IdentityServer, Bolt callers and scopes are least-privilege, event idempotency is payload-aware and concurrency-safe, session lifecycle rules are explicit, migrations own only the Attendance schema, wrappers propagate cancellation, the SQLite advisory is removed, and mandatory CI covers module plus shared authorization/token contracts.

## Current Findings

### Critical

None.

### High

None.

### Medium

None.

### Low

None.

## Finding Resolutions

### H1 - Trusted actor attribution: Resolved

- Event and adjustment operations require an authenticated trusted actor and reject mismatching compatibility actor fields at `Attendance.Api/Services/AttendanceService.cs:453-460` and `:758-763`.
- Persisted actor IDs come from trusted invocation context at `AttendanceService.cs:537` and `:861`.
- Unit coverage includes spoof rejection and trusted actor persistence for events and adjustments.

### H2 - Approved reads after PR #403: Resolved

- All seven Attendance entities intentionally keep generated CRUD disabled while declaring `AuthorizationFeature = "attendance"`; representative metadata is at `Attendance.Domain.Shared/Contracts/AttendanceContext.cs:3-10`.
- Five explicit tenant-scoped read operations replace generic Attendance remote queries under `Attendance.Api/Features/Reads/` and `Attendance.Api/Services/AttendanceReadService.cs`.
- Portal uses those wrappers at `XFramework.Portal/Services/AttendancePortalReadService.cs:23-65` and no longer calls `IgnoreQueryFilters()` for Attendance data.
- The completeness guard at `Attendance.IntegrationTests/Tests/AttendanceGeneratedAuthorizationCompletenessTests.cs:16-55` asserts zero generic read policies, zero mutable entities, actor-required metadata, and no service-only exception.
- Authorization tests cover valid Portal actor access, service-only denial, missing capability, feature-disabled, wrong caller, missing scope, and cross-tenant denial.

### H3 - Participant history and reports: Resolved

- Removal now deactivates and end-dates membership without deleting it at `AttendanceService.cs:221-230`.
- Event/record/adjustment membership checks and report rosters apply `StartedAt`/`EndedAt` to the session start at `AttendanceService.cs:515-521`, `:711-720`, and `:810-816`.
- The explicit session-detail projection applies the same date-effective roster rule in `AttendanceReadService.cs`; tests cover deactivation visibility, historical session detail, and report stability when memberships start or end after a session.

### M1 - Identity credential integrity: Resolved

- `AttendanceCredentialResolver` uses `IIdentityServerServiceWrapper`, maps unavailable IdentityServer responses to a controlled 503, and logs the failure at `Attendance.Api/Services/AttendanceCredentialResolver.cs:22-76`.
- Enrollment verifies credential ID, effective tenant, enabled/deleted state, and copies authoritative alias/username labels at `AttendanceService.cs:170-190`.
- Direct resolver and service tests cover success, missing, wrong tenant, disabled, deleted, authoritative labels, and unavailable IdentityServer.

### M2 - Bolt destination least privilege: Resolved

- Canonical `attendance.read` and `attendance.write` scopes are declared at `XFrameworkServiceScopes.cs:12-13`.
- Every custom handler requires one exact operation scope and the `XFramework.Portal` caller; representative write metadata is at `Features/Contexts/Create/Endpoint.cs:10-12`.
- Contract tests enumerate all 18 handlers and wrapper methods and verify actor/tenant policy, caller, exact scope, cancellation propagation, and service-only denial.
- Compose grants the two scopes to Portal's issuer allowlist without adding them to default token requests.

### M3 - Event idempotency: Resolved

- Replay compares the complete normalized event payload and returns conflict for key reuse with different data at `AttendanceService.cs:1026-1081`.
- Unique-key races are translated into replay/conflict after clearing failed tracked state at `AttendanceService.cs:556-578`.
- PostgreSQL coverage runs two concurrent same-key requests and asserts one persisted event at `AttendancePostgresTests.cs:210-239`.

### M4 - Session and event lifecycle: Resolved

- Session creation validates UTC timestamps, real timezone IDs, chronology, and initial status at `AttendanceService.cs:294-314` and `Features/Sessions/Create/CreateAttendanceSessionValidator.cs:20-38`.
- The transition operation implements `Scheduled -> Open`, `Open -> Closed`, cancellation from scheduled/open, and terminal closed/cancelled states at `AttendanceService.cs:405-445` and `Features/Sessions/Transition/Endpoint.cs:8-21`.
- Events require an open session, cannot be future-dated, and must fall inside the session window at `AttendanceService.cs:502-511`; adjustment chronology and window checks are enforced at `:766-805`.

### M5 - Migration schema ownership: Resolved

- `AddAttendanceBaseline` now creates/drops only Attendance-owned objects and contains no Identity/Application data mutation.
- `AttendanceMigrationOwnershipTests.cs:16-36` reflectively checks both `Up` and `Down` for cross-schema SQL.

### M6 - Mandatory security integration and CI: Resolved

- The PostgreSQL/Bolt integration fixture fails rather than skips in CI when Testcontainers cannot start at `AttendanceIntegrationTestFixture.cs:99-106`.
- `.github/workflows/attendance-integration-tests.yml` runs Attendance unit/PostgreSQL tests plus targeted shared generated-authorization, data-context, source-generator parity, and token-cache suites.
- Runtime tests exercise real PostgreSQL, Bolt envelopes, trusted invocation resolution, feature/capability gates, caller/scope checks, wrapper reads/writes, tenant denial, generic data-context denial, and concurrent idempotency.
- The module fixture uses deterministic token/identity providers; production token acquisition, independent service/Bolt caches, and validation remain covered by the shared Bolt/Core suites invoked by the workflow.

### L1 - SQLite advisory: Resolved

- `Attendance.Tests.csproj:17` pins `SQLitePCLRaw.bundle_e_sqlite3` to the centrally managed patched version.
- `dotnet list ... --vulnerable --include-transitive` reports no vulnerable packages.

### L2 - Wrapper cancellation: Resolved

- All 18 business/read methods accept `CancellationToken` at `AttendanceServiceWrapper.cs:20-55` and pass it through token acquisition, actor-token lookup, and Bolt invocation.

## PR #403 Revalidation

| Previous condition | Current status |
|---|---|
| Remote Attendance reads bypassed generated authorization | **Resolved by PR #403 and retained.** Generic reads fail closed; approved reads use explicit wrappers. |
| PR #403 caused Portal Attendance reads to fail because no generated policies existed | **Resolved.** Portal no longer depends on generic Attendance policies. |
| Generated service-only access could be broader than intended | **Resolved.** No Attendance entity has `AllowGeneratedServiceAccess`; custom reads also require an actor. |
| Capability taxonomy/completeness was absent | **Resolved.** Explicit `attendance:view` read capability and completeness tests are present. |
| Client-selected tenant could cross tenant boundaries | **Resolved and reverified.** Trusted effective tenant mismatch is denied before business execution. |
| Service/Bolt token acquisition could burst independent quotas | **Resolved and reverified.** Shared singleton per-key caches remain independently single-flight; targeted tests pass. |

## Verification Evidence

- `Attendance.Tests`: 37 passed, 0 failed.
- `Attendance.IntegrationTests` through the documented xeon-dev loopback Docker tunnel: 10 passed, 0 failed, including PostgreSQL migration/index checks, wrapper flow, explicit reads, tenant denial, generic `IDataContext` denial, transition, and concurrent idempotency.
- Attendance migration and generated-authorization static guards: 2 passed independently of Docker.
- Core generated authorization/data-context security: 64 passed.
- Source-generator authorization parity: 64 passed.
- Service/Bolt token provider cache tests: 13 passed.
- Portal Attendance contract command: exit code 0; Portal build passed.
- Attendance API and integration-test builds passed with 0 errors.
- Attendance test package vulnerability scan: no vulnerable packages.
- xeon-dev tunnel, remote `socat`, and Testcontainers resources were cleaned up after runtime verification.

## Residual Risk

The Attendance integration suite intentionally substitutes deterministic identity/token providers so it is self-contained; it does not boot a second full IdentityServer process. The production resolver contract and failure mapping are directly tested, while IdentityServer issuance/validation and both token caches are gated by the shared security suites in the same workflow. No actionable Attendance defect remains from this audit.
