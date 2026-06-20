---
title: "Blazor IDataContext Migration"
date: 2026-05-15
category: developer-experience
module: XFramework.Blazor
problem_type: developer_experience
component: service_object
severity: high
applies_when:
  - "Replacing generated per-entity CRUD service wrapper calls in Blazor with IDataContext queries and SaveChanges"
tags: [blazor, datacontext, service-wrapper, migration, crud]
---

# Blazor IDataContext Migration

## Context

Generated per-entity CRUD members were removed from service wrappers.

Examples that no longer exist:

```csharp
identityServerServiceWrapper.IdentityCredential.Get(id)
identityServerServiceWrapper.IdentityCredential.GetList(...)
identityServerServiceWrapper.IdentityInformation.Create(entity)
walletsServiceWrapper.Wallet.GetList(...)
walletsServiceWrapper.WithdrawalRequest.Patch(entity)
```

Service wrappers should now be used for business RPC operations and custom endpoint actions, such as:

```csharp
identityServerServiceWrapper.AuthenticateIdentity(request)
identityServerServiceWrapper.ChangePassword(request)
walletsServiceWrapper.TransferWallet(request)
```

Generic entity query work should use `IDataContext`. Generic entity mutation should use `IDataContext.Add/Update/Remove` only when the entity is intentionally allowlisted for remote mutation and no richer business endpoint exists for that user action. If a request contract or wrapper method exists, prefer the wrapper because it owns validators, feature gates, idempotency, tenant derivation, ledger/allocation logic, and status transitions.

See also:

- `docs/solutions/conventions/ef-core-data-access-patterns.md` for local EF and remote `IDataContext` behavior.
- `docs/solutions/architecture-patterns/decentralized-remote-data-context.md` for generated remote service-wrapper routing.
- `docs/solutions/best-practices/xframework-caching-strategy.md` for remote data-context client cache behavior.
- `docs/solutions/developer-experience/controlpanel-service-wrapper-and-integration-test-contract.md` for the ControlPanel wrapper-vs-`IDataContext` decision rule and required integration-test coverage.

## Migration Target

Replace Blazor generated CRUD wrapper usage with `IDataContext`.

Before:

```csharp
var response = await identityServerServiceWrapper.IdentityCredential.Get(id);
```

After:

```csharp
var credential = await dataContext.Query<IdentityCredential>()
    .Where(x => x.Id == id)
    .FirstOrDefaultAsync(cancellationToken);
```

Before:

```csharp
var response = await walletsServiceWrapper.Wallet.Create(wallet);
```

After:

```csharp
dataContext.Add(wallet);
var result = await dataContext.SaveChangesAsync(cancellationToken);
```

Before:

```csharp
var response = await identityServerServiceWrapper.IdentityInformation.Patch(identity);
```

After:

```csharp
dataContext.Update(identity);
var result = await dataContext.SaveChangesAsync(cancellationToken);
```

## Known Affected Area

The current affected project is:

```text
src/Modules/XFramework.Blazor/XFramework.Blazor.csproj
```

Known usages are in Blazor feature behavior files under:

```text
src/Modules/XFramework.Blazor/Core/Features/**/Behaviors/*.cs
```

Search pattern:

```text
identityServerServiceWrapper.<Entity>.(Get|GetList|Create|Patch|Replace|Delete)
walletsServiceWrapper.<Entity>.(Get|GetList|Create|Patch|Replace|Delete)
```

## Implementation Notes

- Inject `IDataContext` into handlers that currently use wrapper CRUD members.
- Keep service wrapper injections only when the handler still calls custom RPC methods.
- Preserve existing response handling behavior where possible.
- For queries that previously returned `QueryResponse<PaginatedResult<T>>`, build the `PaginatedResult<T>` locally after querying.
- For commands that previously returned `CmdResponse<T>` or `CmdResponse`, adapt from `DataContextResult` where the handler expects response objects.
- Use `Include(...)` on `IDataContext.Query<T>()` for existing `includes` arguments.
- Use `.Skip(...)` and `.Take(...)` for paging.
- Use `.Where(...)` expressions instead of manually building `QueryFilter` lists where practical.
- Keep custom business wrapper calls unchanged.
- Do not replace business wrapper calls with direct `IDataContext` saves.

## Build Verification

After migration, run:

```bash
dotnet build src/Modules/XFramework.Blazor/XFramework.Blazor.csproj
```

The expected current failure before migration is missing members on `IIdentityServerServiceWrapper` and `IWalletsServiceWrapper`, such as `IdentityCredential`, `IdentityContact`, `Wallet`, and `WithdrawalRequest`.
