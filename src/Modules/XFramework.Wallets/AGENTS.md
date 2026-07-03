# Wallets Module Agent Guide

This file applies to `src/Modules/XFramework.Wallets/**`. Start with the root
[`AGENTS.md`](../../../AGENTS.md) and [`CLAUDE.md`](../../../CLAUDE.md), then use
this file for Wallets-specific rules.

## Module Purpose

Wallets owns tenant-scoped wallet accounts, balances, deposits, withdrawals,
transfers, holds, ledger postings, policy and fee rules, maker-checker approvals,
refund/dispute cases, payment webhooks, outbox dispatch, reconciliation, and
reporting.

This is a money-moving module. Treat every balance mutation as financial state,
not ordinary CRUD.

## Project Map

- `Wallets.Domain.Shared` contains contracts, generated endpoint metadata,
  enums, EF configurations, request DTOs, and response DTOs.
- `Wallets.Api` contains service implementations, endpoint slices, validators,
  installers, hosted services, and generated endpoint registration.
- `Wallets.Integration` contains generated/custom Bolt service wrapper contracts
  used by other modules and Portal.
- `src/Tests/Wallets.IntegrationTests` contains integration and contract tests.
- Portal Wallets surfaces live under
  `src/Presentation/XFramework.Portal/Components/Pages/Finance`.

## Source Of Truth For Money

- `WalletLedgerService` is the financial write path. New money mutations must go
  through ledger execution so operations, postings, snapshots, transactions, and
  outbox messages stay consistent.
- `WalletOperation`, `WalletLedgerEntry`, and `WalletBalanceSnapshot` are the
  audit/reconciliation backbone. Do not introduce a second balance source of
  truth.
- Existing `WalletTransaction` rows are compatibility/read-model records. Do not
  build new financial behavior that treats mutable transaction rows as the
  authoritative ledger.
- Fees must be explicit calculated values/postings. Do not trust arbitrary
  client-supplied fee amounts unless the workflow explicitly allows and verifies
  the override.

## Integration Rules

- Prefer generated `IWalletsServiceWrapper` methods from `IBoltRequest`
  contracts for cross-module and Portal calls.
- Add new workflow actions as request/response DTOs under
  `Wallets.Domain.Shared.Contracts.Requests` and `Responses`, then expose them
  with `[BoltHandler]` and `[MapPost]`/`[MapGet]` endpoint slices.
- Keep public/manual endpoints DTO-backed. Avoid returning EF entities from new
  public endpoints because navigation cycles break OpenAPI and leak internal
  model shape.
- Use `Result<T>` consistently for service and endpoint outcomes.
- Keep tenant, actor, credential, correlation id, IP, and user-agent resolution
  server-side through `IWalletRequestContextResolver` and trusted metadata.
- Do not trust `TenantId`, actor, credential, fee, status, or provider result
  values from request bodies when the server can resolve or verify them.
- Use the tenant feature taxonomy already wired in `Wallets.Api/Program.cs`:
  `wallets`, `wallets.transfers`, `wallets.deposits`, `wallets.withdrawals`,
  `wallets.batch`, `wallets.reconciliation`, `wallets.policy`,
  `wallets.webhooks`, and `wallets.reporting`.

## Workflow Rules

- Deposits and withdrawals are workflows: create, validate, approve/reject,
  settle/complete, fail, cancel, and expire. Do not mutate workflow status
  directly to bypass validation or ledger posting.
- Payment webhooks must be signature-validated, idempotent by provider event or
  external reference, raw-payload audited, and retry-safe.
- Outbox publishing must be durable: poll pending messages, retry with backoff,
  capture errors, and move exhausted messages to failed/dead-letter states.
- Reconciliation must compare balances, snapshots, running ledger, transaction
  records, and provider status before marking drift as resolved.
- Maker-checker approvals are required for sensitive operations such as large
  transfers, withdrawals, manual adjustments, reversals, and freeze/unfreeze.
- Refunds, disputes, chargebacks, and reversals must link back to the original
  operation, transaction, external reference, or case when available.

## Database And Concurrency Rules

- Keep ledger execution inside one database transaction.
- Lock affected wallets in deterministic order before mutating balances.
- Preserve idempotency protections: same tenant + idempotency key should replay
  the same request, and the same key with a changed request hash should fail.
- Maintain constraints and indexes for account numbers, active wallet uniqueness,
  provider event/reference uniqueness, outbox polling, statements, operation
  history, and reconciliation.
- Never use ad hoc balance updates in batch or workflow code. Use ledger-backed
  service methods and verify snapshot/posting reconciliation.

## Portal Rules

- Portal mutations must call Wallets service wrappers or backend workflow
  APIs. Read-only `IDataContext` queries are acceptable for grids, details,
  pickers, and display labels only.
- Do not add direct `DataContext.Add`, `Update`, `Remove`, or
  `SaveChangesAsync` calls for Wallets financial entities in Portal.
- Use shared `XfEntityPicker<TItem>` with useful Wallets-specific Advanced
  Search columns and filters when admins select wallets, credentials, wallet
  types, currencies, gateways, operations, approvals, cases, or reconciliation
  items.
- Follow `rules/UiGuidelines.md`: `BbDataGrid` for data-heavy UI, filterable
  user-facing columns, empty states, safe toasts, confirmations for sensitive
  actions, typed money/numeric controls, and tabs for pages with multiple grids.
- Keep raw provider payloads, stack traces, full exception messages, and full
  GUIDs out of primary visible labels. Log details and show safe summaries.

## Do

- Read representative Wallets services and tests before editing.
- Add or update validators for new request DTOs.
- Add focused integration tests for new workflow behavior, idempotency, tenant
  spoofing, policy rejection, fee calculation, outbox retry, reconciliation, and
  concurrency risk.
- Add or update wrapper coverage tests when adding custom `IBoltRequest`
  methods.
- Keep OpenAPI inclusion safe by using DTOs for public/manual routes.
- Preserve existing source-compatible API contracts where practical.
- Update this `AGENTS.md` whenever Wallets feature behavior, integration rules,
  workflow invariants, security assumptions, or bug-fix learnings change.

## Do Not

- Do not introduce direct balance mutation outside `WalletLedgerService`.
- Do not expose generated create/update/delete CRUD as the mutation path for
  financial entities.
- Do not trust client-supplied tenant, actor, credential, fee, status, or
  provider outcome fields for money operations.
- Do not swallow ledger, webhook, outbox, or reconciliation failures without
  durable audit state.
- Do not add raw tables, native selects, custom table components, or direct
  financial `IDataContext` mutations in Portal.
- Do not leak sensitive financial diagnostics in UI errors, toast messages, or
  logs.

## Verification Commands

Run the narrowest relevant tests for your change, then expand by risk:

```powershell
dotnet build src/Modules/XFramework.Wallets/Wallets.Api/Wallets.Api.csproj -m:1 /nr:false -v:minimal
dotnet build src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj -m:1 /nr:false -v:minimal
dotnet test src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj -m:1 /nr:false --logger "console;verbosity=minimal"
```

For Portal-only Wallets changes, also run:

```powershell
dotnet build src/Presentation/XFramework.Portal/XFramework.Portal.csproj -m:1 /nr:false -v:minimal
dotnet test src/Tests/Wallets.IntegrationTests/Wallets.IntegrationTests.csproj --filter "TestCategory=Area:PortalContract" -m:1 /nr:false --logger "console;verbosity=minimal"
```
