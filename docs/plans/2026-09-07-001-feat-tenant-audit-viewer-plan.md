---
title: "feat: Tenant-enabled Audit module and detailed Portal viewer"
type: feat
status: active
date: 2026-09-07
---

# Tenant-enabled Audit module and Portal viewer

## Implementation refinements

- Use the existing permission vocabulary, `audit:view`, instead of introducing `audit.logs:read`.
- Reuse the query contract with an authorized anchor for record/transaction/correlation history.
- Keep wrapper implementation in the established `Audit.Integration` project.
- Initial source disclosure rules are recorded in the [module README](../../src/Modules/XFramework.Audit/README.md).

## Objective and proposed decision

Add an **Audit** module that a platform administrator can enable in **Identity → Tenants → Modules**, matching the existing module tree shown in the supplied screenshot. Authorized tenant administrators can then investigate who changed a record, what changed, and when through the Portal.

Use the existing centralized `audit.event` store. Keep capture in the shared runtime and PostgreSQL triggers. The new module owns authorized investigation queries and presentation; it does not implement another recorder. Turning off the module, or stopping its API, must never disable capture or remove history.

This is a proposed implementation plan only. No runtime changes, tenant enablement, or deployment are part of this planning step.

## Source findings

The following paths were inspected in the current checkout. Source behavior takes precedence over historical plans.

| Existing surface | Reuse and implication |
|---|---|
| [AuditEvent](../../src/Kernel/XFramework.Domain/Auditing/AuditEvent.cs) and [configuration](../../src/Kernel/XFramework.Domain/Auditing/AuditEventConfiguration.cs) | Already contain time, actor IDs/kind, service, tenant before/after, entity key, changed fields, old/new JSON, transaction ordinal, correlation, and trace metadata. The entity does **not** implement the standard tenant interface: every read requires an explicit authorized predicate. |
| [Central audit migration](../../src/Kernel/XFramework.Domain/Migrations/20260906164811_AddCentralAuditTrail.cs) | Supplies append-only capture, redaction, and trigger coverage. Preserve these guarantees. |
| [TenantModuleFeatureKeys](../../src/Modules/XFramework.IdentityServer/IdentityServer.Domain.Shared/Contracts/TenantModuleFeatureKeys.cs) | Built-in module catalog has no Audit entry. Add one disabled by default. |
| [TenantModuleFeatureService](../../src/Kernel/XFramework.Core/Services/FeatureGates/TenantModuleFeatureService.cs) | Missing enablement is false; authorization reads are deliberately uncached. Use the existing gate. |
| [SetModuleFeatures endpoint](../../src/Modules/XFramework.IdentityServer/IdentityServer.Api/Features/Tenants/SetModuleFeatures/Endpoint.cs) | Module management already has a privileged wrapper workflow and concurrency validation. Reuse it. |
| [TenantDetail](../../src/Presentation/XFramework.Portal.Features.Identity/Pages/TenantDetail.razor) and [definition resolver](../../src/Presentation/XFramework.Portal.Features.Identity/Services/TenantModuleFeatureDefinitionResolver.cs) | Existing tree, descriptions, dependencies, and coverage labels can display Audit. Coverage labels include explicit mappings that must be updated. |
| [TenantModuleNavigationService](../../src/Presentation/XFramework.Portal/Services/TenantModuleNavigationService.cs) | Supplies concrete-tenant navigation. Its reload path currently returns early for the same tenant; verify and fix invalidation as needed so toggles take effect without signing out. |
| [PortalFeatureAssemblies](../../src/Presentation/XFramework.Portal/Composition/PortalFeatureAssemblies.cs) | Register the new feature RCL once for both router and endpoint discovery. |
| [Actor capabilities](../../src/Shared/XFramework.Domain.Shared/ServiceIdentity/XFrameworkActorCapabilities.cs) and [service scopes](../../src/Shared/XFramework.Domain.Shared/ServiceIdentity/XFrameworkServiceScopes.cs) | Extend existing authorization infrastructure; a service token alone must not confer a human administrator's audit access. |

Follow [backend rules](../../rules/BackendGuidelines.md), [UI rules](../../rules/UiGuidelines.md), [Portal RCL boundaries](../solutions/developer-experience/portal-feature-rcl-architecture.md), and the [wrapper contract](../solutions/developer-experience/portal-service-wrapper-and-integration-test-contract.md). This extends the [central audit plan](../architecture/centralized-audit-trail-plan.md).

## Scope

The first release includes tenant enablement, admin authorization, a searchable paginated event list, detailed field comparisons, record history, and related changes in the same transaction/request. It supports existing and newly captured events.

Do not include editing/deleting audit events, undoing business changes, configurable retention, bulk export, real-time streaming, a second audit database, or a replacement capture pipeline. These are separate features with separate requirements. Authentication logs and wallet-specific audit screens remain distinct sources; this screen is explicitly labeled **Database changes**.

## Architecture

Proposed projects follow the repository's normal module layout:

- `src/Modules/XFramework.Audit/Audit.Api`: independently deployed read API, VSA endpoints, query service, validators, authorization, and feature gating.
- `src/Modules/XFramework.Audit/Audit.Domain.Shared`: request/response contracts and the standard generated wrapper definition. No duplicate `AuditEvent` entity or generic entity CRUD registration.
- `src/Presentation/XFramework.Portal.Features.Audit`: list/detail routes and feature-specific presentation logic, referencing Portal.Shared and module contracts/wrapper only.
- `src/Tests/Audit.IntegrationTests`: PostgreSQL and wrapper/security integration coverage, using existing test conventions.

The API reads the existing audit schema in the one shared PostgreSQL database through `AppDbContext`. Document this intentional query ownership in module documentation; capture and migrations remain owned by shared persistence. Do not relocate the existing EF model simply to match a new project name.

Portal calls the generated Audit wrapper over the current Bolt/trusted invocation infrastructure. It must not query `audit.event` through generic remote `IDataContext`, depend on the API implementation assembly, or connect directly to PostgreSQL.

A separately deployed reader fits the existing service topology and can fail without affecting transactional capture. Add its ordinary service discovery, service identity grants, container/build/deploy configuration, and health checks. Reuse current templates and composition conventions; do not add a queue or ingestion worker.

## Tenant enablement and permissions

1. Add module key `audit`, display name **Audit**, description **View database change history, actors, and field-level changes**, using an existing Lucide history/shield icon. One module switch is enough for this release; list/detail/history do not need separate feature flags.
2. New and existing tenants remain disabled until explicitly enabled through the existing tenant module management workflow. No blanket seed migration.
3. Enabling exposes retained history for that tenant, including events recorded before enablement. Disabling removes viewer access immediately on subsequent backend requests while capture continues.
4. Keep module administration under existing tenant-management authorization. Tenant audit readers cannot enable themselves or change `audit.table_policy`.
5. Introduce actor capability `audit.logs:read` and service scope `audit.read` through the existing capability evaluation, role assignment, and service grant mechanisms. Grant the actor capability only to the established tenant-admin and SuperAdmin roles after verifying their actual definitions. Ordinary users receive no grant. Do not infer privilege from a UI label or client-supplied role.
6. Require an authenticated actor, verified tenant access, `audit.logs:read`, and enabled `audit` for every investigation operation. Bolt additionally requires a correctly scoped destination service token and trusted actor delegation; REST must enforce equivalent checks.
7. First release requires a concrete selected tenant, including for SuperAdmin. In **All Tenants**, show “Select a tenant to view audit logs.” No implicit global query or SuperAdmin gate bypass.
8. Hide navigation when unavailable; protect direct URLs and all backend methods independently. Return an appropriate forbidden/module-disabled state without exposing event existence.

## Tenant isolation and sensitive data

One shared query/projection policy must protect list, detail, record history, related events, and filter options.

- Derive the effective tenant from the verified invocation. A request tenant selector is only a target to authorize, never evidence of access.
- Normal row ownership is `SubjectTenantIdBefore == authorizedTenant` or `SubjectTenantIdAfter == authorizedTenant`. Actor tenant and effective tenant are diagnostic metadata, not sufficient proof that the changed data belongs to that tenant.
- For records transferred between tenants, return only the before/after snapshot belonging to the caller's tenant. Suppress the other snapshot, other-tenant IDs, and cross-boundary diff information; show “Other tenant data restricted.” Never deliver both payloads and rely on browser hiding.
- When only one side is authorized, derive visible field information from that side; do not leak the hidden side through changed-field names, labels, entity links, or related-event counts.
- Rows with no reliable subject tenant are excluded from this tenant viewer by default. Inventory tenant-root and child/join tables during implementation: add narrowly tested ownership rules only when the stored event itself proves ownership. In particular, validate the actual tenant table/schema before treating its primary key as a tenant ID. Do not infer historical ownership from a mutable current parent record. Unresolved/global rows remain outside this release's viewer.
- Explicitly test deletes, soft deletes, transfers, nested ownership, unknown schemas, and null tenant fields. Enabling the viewer does not justify broad `IgnoreQueryFilters()` access.
- Preserve capture-time redaction and binary omission. Apply a server-side presentation allowlist/redaction policy for especially sensitive identity/security tables before returning DTOs; capture's name-based secret redaction alone is not a full disclosure policy. Unknown tables expose safe metadata only until their payload policy is reviewed.
- Never return credentials, tokens, private keys, or unreviewed authentication payloads. Do not offer a “reveal secrets” control. A JSON view uses the same sanitized data as the field comparison.
- Resolve actor display names through approved Identity query/wrapper paths, in batches where supported, restricted to authorized identities. IDs and actor kind remain authoritative. Current names must be labeled as current, not historical snapshots.
- If no actor ID was captured, show the recorded System/Service/Unknown kind and service; do not guess the human from a database user or tenant. Deleted/unresolvable identities retain a clear ID fallback.
- Omit client IP, user agent, database username, and instance details from the normal tenant payload initially; expose only necessary event, transaction, correlation, and trace identifiers in technical details. A later diagnostics permission can expand this deliberately.

## Portal experience

### Tenant module management

Add Audit to the current module tree and preview. Display **API gated** only once enforcement exists. Explain next to its description that the switch controls access to history and does not stop recording. Refresh availability after a successful toggle, including another open page's next request. Preserve existing concurrency-conflict handling.

### Event list: `/audit/logs`

Use the Portal page header, selected tenant indicator, refresh action, and `BbDataGrid` with server-side `ItemsProvider`. Initial page size 25, maximum 100. No full audit payloads in list responses.

Columns: recorded time, actor, action (Created/Updated/Deleted/Soft deleted), source module/service, record type, readable record key, and changed-field summary. Use an explicit “Unknown” fallback for unmapped sources. IDs are available in detail/copy affordances rather than dominating the list.

Filters: UTC-backed date range (default last seven days), operation, schema/table or module mapping, service, actor ID/kind, exact record key, changed field, correlation ID, and transaction ID. Provide clear/reset controls. Use native `Filterable="true"` columns and `FilterBy` on template columns matching rendered values; action columns are unfiltered. Translate only supported filter/sort fields server-side. Do not imply actor-name filtering works if only actor IDs are searchable.

Click a row for detail. Preserve filter/page state on back navigation. Cancel requests and clear list/detail immediately on tenant switch; discard late responses from the old tenant. Show loading, empty-range, module-disabled, forbidden, and service-unavailable states distinctly. Use shared/Blueprint controls and verify light/dark, narrow layout, and keyboard access.

### Event detail: `/audit/logs/{eventId:long}`

Header: action, record type/key, recorded time in local time with explicit timezone and UTC precision, actor label/kind, and originating service.

Primary content is a `BbDataGrid` field comparison: **Field | Before | After | Change**. Default to changed fields, with an option to show other authorized fields. Render insert/delete absent sides as “Not present,” distinguish JSON null from empty string and missing properties, and show redacted/omitted/restricted values honestly. Do not use color as the only indicator.

Use expandable formatted values for objects/arrays, escaped text rendering, and a sanitized JSON tab. Preserve number precision; do not coerce arbitrary JSON numbers through floating point. Nested objects can be shown as changed field values initially, avoiding a custom recursive diff engine. Bound displayed value sizes with explicit truncation; do not silently lose content.

Technical details include event ID/UUID, transaction and ordinal, correlation/change-set identifiers when present, and trace/span IDs. A **Record history** action reuses the list filtered by exact schema/table/complete composite key. **Related changes** shows bounded, tenant-authorized events for the transaction or correlation. Missing identifiers disable the corresponding action.

“Recorded at” is the trigger recording time, not a guaranteed commit timestamp. Transaction ordinal describes order within a transaction; sequence IDs and wall-clock time do not establish a global commit order across concurrent transactions. Related request events may span transactions and services. Explain this in the detail help text.

## API and query contracts

Proposed read operations (final method naming follows wrapper generation conventions):

| Operation | Contract and result |
|---|---|
| SearchAuditEvents | Validated date/filter/sort/page request; projected summaries, bounded page, filtered total. |
| GetAuditEvent | Event ID; sanitized detail, field comparison, safe actor/source labels. |
| GetAuditRecordHistory | Exact schema/table and complete entity key; same pagination and authorization as search. Can reuse Search internally. |
| GetRelatedAuditEvents | Authorized anchor event ID plus transaction/correlation mode; derive identifiers from the anchor, then apply the same tenant filter. |

Prefer fixed source/filter metadata from a reviewed mapping; add a bounded filter-options endpoint only if the UI needs dynamic options. Never enumerate all tenant users or all distinct audit values without limits.

Use `IBoltRequest`, metadata, generated wrapper/handlers, concrete validators, thin endpoints, `Result<T>`, and cancellation tokens. Authorize before querying by event ID; unauthorized/missing detail must be indistinguishable. No mutation endpoints or generated CRUD attributes on AuditEvent.

Keep filters and projection in PostgreSQL. Whitelist columns/operators and parameterize JSON-key queries. Reuse existing tenant-before/after, schema/table/event, actor, correlation, JSON-key GIN, and time BRIN indexes; inspect actual execution plans before adding narrowly targeted indexes through MigrationRunner.

Use bounded page-number pagination supported by `BbDataGrid`, deterministic secondary `EventId` sorting, maximum date range of 90 days per search initially, and configured query timeout. Large histories can be examined in successive time windows. Do not implement live insertion into a user's current page; Refresh reloads it. Offset pages are not a transactionally stable snapshot under concurrent writes, and must not be advertised as such. Evaluate seek pagination only if measured paging cost warrants a supported UI adaptation.

Avoid shared caching of audit payloads. Log query latency/result count and denial outcome without old/new values. Do not claim database SELECTs are captured by row triggers; a durable audit-of-audit-read trail would need a separate explicit design.

## Implementation sequence

1. **Confirm integration points:** refresh the implementation worktree against current development code; inventory actual admin grants, service provisioning, generated wrapper configuration, and audit source table ownership/disclosure policies. Record gaps in actor/tenant attribution instead of disguising them in UI.
2. **Module contracts and read API:** create module projects, trusted authorization/capability/scope integration, shared tenant predicate and safe projection, four read operations, validation, and PostgreSQL tests. Cover all current module table families in a reviewed visibility matrix, documenting metadata-only/excluded exceptions.
3. **Tenant catalog and availability:** register `audit` disabled by default, preserve module-management authorization, add discovery/coverage metadata, and verify same-tenant toggle invalidation. Tests must prove capture continues while disabled.
4. **Portal feature:** add Audit RCL, marker/catalog/project registrations, wrapper composition, gated navigation, list, field detail, history, and related changes. Update architecture/catalog tests for the new project.
5. **Verification:** run API, wrapper, gate/permission, PostgreSQL, Portal contract and browser tests. Review query plans on representative audit volume. Confirm no new generic read/mutation escape path.
6. **Coordinated deployment after implementation is requested:** inspect open PRs and active Xeon Dev workflows, use the existing deployment concurrency control, deploy only once the environment is stable, and enable Audit only for the designated test tenant through the existing admin workflow. Verify the UI with real database changes and a second tenant. Preserve volumes and existing business records; retain intentional audit test evidence.

## Acceptance tests

- Audit appears in Tenant Modules and is disabled without a stored enablement row. Authorized management can toggle it with concurrency checks.
- Tenant admin + enabled module can list/detail/history; ordinary users, missing actors, invalid service tokens, wrong scopes/capabilities, and disabled tenants are denied over both REST and Bolt.
- Switching or forging tenant IDs, guessing event IDs, and using transaction/correlation links cannot reveal another tenant's values, identifiers, names, or counts.
- Tenant transfers expose only the authorized side; deletes remain visible from before-tenant ownership; null/global ownership is excluded unless a reviewed rule proves it.
- New inserts, updates, deletes, soft deletes, bulk/raw SQL writes appear with the captured attribution and correct values. Rollbacks do not appear. Unknown actors remain explicitly unknown.
- Secrets stay redacted in DTOs, list summaries, field comparisons, JSON, filters, errors, and logs. Unreviewed table payloads are withheld.
- Composite keys, JSON null/missing/empty values, large values, nested JSON, precise numbers, deleted actors, and unmapped services render correctly.
- Grid filtering/sorting/pagination execute on the server; tenant switches discard stale responses. Date boundaries and displayed timezones are tested.
- Disabling the module or stopping Audit.Api does not stop business audit capture. Re-enabling restores access to retained history. No viewer action mutates the audit store.
- Portal build, architecture/route tests, exact wrapper integration coverage, and browser tests pass. In browser testing, verify the module toggle, privileged/ordinary users, two-tenant isolation, direct URLs, unavailable reader, list-to-detail navigation, and both themes.

## Completion definition and limits

Complete when a platform admin can enable Audit for a tenant, that tenant's authorized admins can investigate database changes through the Portal, and automated/live verification demonstrates tenant isolation and uninterrupted capture.

The viewer cannot recover historical changes from before audit capture existed, actor identities never captured, redacted values, or omitted binary content. The current shared privileged database role and off-host archival hardening remain the separate work identified in the original capture plan. Neither should be represented as solved by adding a viewer.
