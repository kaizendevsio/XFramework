# Attendance Module Agent Guide

This file applies to `src/Modules/XFramework.Attendance/**`.

Update this file in the same PR whenever Attendance behavior, contracts, deployment, integration rules, UI expectations, bug fixes, or production lessons change. Treat stale guidance here as a module bug.

## Read First

- Start with the repository root `AGENTS.md` and `CLAUDE.md`.
- Read `rules/BackendGuidelines.md` before changing services, EF entities, configurations, migrations, wrappers, Bolt handlers, caching, or runtime setup.
- Read `docs/solutions/conventions/xframework-best-practices.md` before backend implementation work.
- Read `docs/solutions/developer-experience/portal-service-wrapper-and-integration-test-contract.md` before changing wrappers, remote `IDataContext`, or Portal integration.
- Current source code wins over this guide. If source and this guide disagree, fix the guide as part of the change.

## Purpose

Attendance is a reusable tenant-scoped engine for school, HR, project, gate, event, and other time-monitoring workflows. Keep it generic. Do not bake in school-only or HR-only concepts unless the domain model has been intentionally expanded.

The mental model is:

- `AttendanceContext`: the reusable container, such as a class, shift group, project, event, gate, or department.
- `AttendanceParticipant`: the link between an IdentityServer credential and an attendance context.
- `AttendanceSession`: the concrete time window where attendance is measured.
- `AttendanceEvent`: the append-only event log for check-in, check-out, API, or manual capture.
- `AttendanceRecord`: the current computed participant status for one session.
- `AttendancePolicy`: grace period, checkout requirement, late, and early checkout rules.
- `AttendanceAdjustment`: audited manual correction with actor and reason.

## Project Map

- `Attendance.Api`: VSA endpoints, validators, service implementation, runtime setup, health checks, and generated Bolt handler registration.
- `Attendance.Domain.Shared`: entities, EF configurations, enums, request contracts, and response contracts shared by API, tests, wrappers, and consumers.
- `Attendance.Integration`: `IAttendanceServiceWrapper` and wrapper registration for Portal and cross-module callers.
- `Attendance.Tests`: focused service/unit tests.
- `src/Tests/Attendance.IntegrationTests`: PostgreSQL-backed module integration tests, wrapper tests, remote `IDataContext` tests, and deployed-shape safety tests.
- `src/Tests/Portal.E2ETests/AttendancePortalContractTests.cs`: Portal read/display contract coverage for Attendance.

## Ownership Boundaries

- Attendance owns only the `Attendance` database schema.
- IdentityServer remains the source of truth for identities and credentials.
- Store identity links as IDs, primarily `TenantId` and `CredentialId`.
- Do not copy or own IdentityServer user records.
- Do not write IdentityServer schema data from Attendance.
- Do not add cross-schema foreign keys in V1 without an explicit architecture decision.
- All data access must be tenant-scoped. Explicitly set or validate `TenantId` on request handling paths.
- Attendance APIs are gated by `TenantModuleFeatureKeys.Attendance`.

## API And Service Rules

- Use Vertical Slice Architecture feature folders under `Attendance.Api/Features`.
- Public business request contracts live in `Attendance.Domain.Shared/Contracts/Requests`.
- Wrapper-callable requests must implement `IBoltRequest`.
- Generated endpoints should use the local `[Map*]` attributes and `[BoltHandler]` where the operation is exposed through the wrapper.
- Keep business logic in `AttendanceService`; endpoint handlers should validate, call the service, and map results.
- `AttendanceService` should return `Result<T>` or `Result`, not throw for expected business failures.
- Validators belong beside their feature endpoint.
- Use projections and pagination for list/report queries.
- Cache only stable reference reads when invalidation is simple and obvious.

## Bolt And Wrapper Rules

- Cross-module and Portal business operations must go through `IAttendanceServiceWrapper`.
- Use wrapper methods for creating or updating contexts, participants, sessions, attendance events, adjustments, and reports.
- Do not perform direct remote `IDataContext` mutations from Portal or another module for Attendance business actions.
- Remote `IDataContext.Query<T>()` is acceptable only for tenant-scoped read projections that are covered by integration or Portal contract tests.
- Keep `Attendance.Api/Program.cs` explicit generated Bolt registration:
  `BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory)`.
- Pass `hostEnvironment` to `AddXFrameworkBoltClient` so non-Development startup validates secure `wss://` transport configuration. Do not bypass that validation with the environment-free overload or a plaintext client URL.
- Do not remove the explicit registration just because REST endpoints still work. Production wrapper calls can fail even when HTTP routes are healthy if Bolt handlers are not registered.

## Time, Events, And Status

- Store and send attendance times as UTC `DateTime` values for `StartsAt`, `EndsAt`, `OccurredAt`, `FromUtc`, and `ToUtc`.
- Avoid `DateTimeKind.Unspecified` at API boundaries. Normalize before comparing or displaying.
- `AttendanceSession` defines the scheduled window. Actual time-in/time-out is recorded through `RecordAttendanceEventRequest`.
- `AttendanceEvent` is append-only and idempotent by tenant plus `IdempotencyKey`.
- Generate a unique, deterministic-enough idempotency key per UI or device action. Portal keys should stay prefixed with a Portal-specific value.
- Duplicate idempotency-key replays should return the existing event outcome, not create another event.
- Check-in after the grace period becomes `Late`.
- Missing checkout can become `Incomplete` when the policy requires checkout.
- Manual adjustments must include `ActorCredentialId` and a non-empty reason.
- Manual check-in/check-out from operators should use `AttendanceEventSource.Manual` and the scoped operator credential from `RequestMetadata.CredentialId`.

## Portal Integration Rules

- Attendance navigation and pages must be feature-gated by `TenantModuleFeatureKeys.Attendance`.
- Portal writes must call `IAttendanceServiceWrapper`.
- Portal read-heavy screens may use `IDataContext.Query<T>()` through `AttendancePortalReadService` when tenant-scoped and tested.
- Use `XfEntityPicker<IdentityCredential>` or an equivalent credential picker for participant selection. Never make operators type credential GUIDs.
- Copy `DisplayName` and `ReferenceCode` from the selected credential for participant display, but treat IdentityServer as authoritative.
- Session rosters should show active context participants. If a participant has no `AttendanceRecord` for the session, display `Absent` without creating a record.
- Use BlazorBlueprint `BbDataGrid` for list, roster, and report UI. Do not introduce raw tables or custom table components for tabular Attendance UI.
- User Detail Attendance views are read-only in V1; operational changes belong in the Attendance workspace.

## Remote DataContext Footguns

- Be careful with remote `IDataContext` expression serialization.
- Do not push `DateTime` constants into remote Attendance predicates unless a deployed-shape integration test proves that query shape works.
- For Portal session date filters, prefer bounded tenant/context/status reads followed by normalized in-process UTC filtering, or use wrapper/report endpoints.
- If a remote query works locally but fails on xeon-dev, add coverage in `Attendance.IntegrationTests` or `Portal.E2ETests` before shipping the fix.

## EF And Schema Rules

- EF entities and configurations live in `Attendance.Domain.Shared`.
- Use the `Attendance` schema for all module tables.
- Keep indexes aligned with tenant, context, session, credential, and date-range query shapes.
- Do not add navigation dependencies that require cross-module schema ownership.
- When changing EF mappings, update migrations/model snapshots through the approved migration flow and verify the migration runner still loads `Attendance.Domain.Shared`.

## Deployment Rules

- The xeon-dev service name is `attendance`.
- The service readiness endpoint is `http://localhost:5182/health/ready` in the dev deployment workflow.
- Attendance changes flow through the single full-stack `.github/workflows/deploy-xeon-dev.yml` deployment.
- Docker runtime settings live with `Attendance.Api`, including `appsettings.Docker.json`, and the root `docker-compose.yml` service definition.
- If deployment, Bolt connectivity, health checks, ports, or compose wiring change, update this guide and add or update integration coverage.

## Testing Expectations

- Add or update `Attendance.Tests` for service rules, state transitions, idempotency, policies, tenant validation, and manual adjustments.
- Add or update `Attendance.IntegrationTests` for PostgreSQL mappings, migrations, wrapper calls, remote `IDataContext` query surfaces, and production-like Bolt registration.
- Add or update Portal contract tests when Attendance read/display projection behavior changes.
- For deployment or wrapper fixes, verify both HTTP endpoint behavior and wrapper/Bolt behavior.
- Documentation-only changes do not require a full build, but run `git diff --check` and check links/paths you reference.

## Do

- Keep Attendance generic and tenant-scoped.
- Use `AttendanceService` as the business authority.
- Use wrapper methods for writes and business operations.
- Validate tenant identity before reading or writing data.
- Preserve idempotency behavior for event capture.
- Update this guide when bugs reveal new operational constraints.

## Do Not

- Do not write IdentityServer data from Attendance.
- Do not require raw credential GUID entry in operator UI.
- Do not bypass `IAttendanceServiceWrapper` for business writes from Portal or another module.
- Do not remove generated Bolt handler registration from `Program.cs`.
- Do not add untested remote `IDataContext` mutation surfaces.
- Do not assume a session itself is the time-in/time-out record; actual attendance capture is an event operation.
