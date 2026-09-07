# Audit investigation module

Audit.Api serves tenant-authorized reads from the shared PostgreSQL audit store.
It never records, changes, or deletes audit events. Disabling the tenant module
or losing this service does not affect transactional capture.

## Access

Enable `audit` in Identity → Tenants → Modules. Grant `audit / view` to the
appropriate administrator role using the existing role-permission editor.
The migration grants view to established admin role types identified by the
existing admin SystemReferenceId; new/custom roles require an explicit grant.
Tenant default-allow policy does not grant audit access. The Portal bootstrap
continues managing its own admin role permissions.

The capability is evaluated independently of the actor's home-tenant module
switch so a platform administrator can select another tenant. Audit.Api always
checks the selected tenant's module switch. Delegated access also requires the
existing tenant-management capability; service calls require `audit.read`.

## Ownership and disclosure

Every list/detail/history query starts with subject-tenant-before/after ownership.
Global and unattributed-ownership events are excluded, including tenant-root
records without captured subject tenant. Tenant module configuration rows carry
TenantId and are visible. There is no current-parent lookup to infer past ownership.

The exact table allowlist in `AuditPresentation` covers operational catalog,
inventory, balance/ledger, POS, attendance, and file metadata tables. Identity,
Communications, Bolt, supplier/contact data, payment/provider payloads, outbox,
and unknown tables are metadata-only. Add tables only after a disclosure review.
Nested secrets are redacted again on read. Cross-tenant transfers return only the
authorized snapshot, with no hidden-side field names in summaries. Composite key
components other than Id are restricted in presentation; record history uses an
authorized anchor's complete key inside the service.

The actor name query is an intentional read model against IdentityCredential:
only IDs already visible in the authorized tenant, at most one page, selecting
current username only. No cross-module writes or identity payloads are exposed.
Deleted/missing/cross-tenant actors retain the captured kind and safe ID fallback.

Search uses bounded server pagination and a maximum 90-day window. Record history
and related changes reuse the same search operation with an authorized anchor;
they do not have separate authorization implementations. The list never fetches
old/new payloads. Recorded time is not commit time; ordering between concurrent
transactions is not guaranteed.

## Runtime

Compose service `audit` uses port 5185 by default, the standard Bolt service
identity, and the existing shared database. Provision the new dev secret with
`scripts/provision-audit-dev-secret.sh` as the protected env owner. MigrationRunner
remains the sole migration authority. The viewer needs no remote IDataContext
registration and publishes no CRUD endpoints.

## Verification

`dotnet test src/Tests/Audit.IntegrationTests/Audit.IntegrationTests.csproj`
requires Docker/Testcontainers and checks PostgreSQL filtering, tenant isolation,
transfer sanitization, permission/gate rejection, and request limits.
The Portal contract suite checks feature routing, dependency boundaries, and
deployment/service identity configuration. Browser verification follows deployment.
