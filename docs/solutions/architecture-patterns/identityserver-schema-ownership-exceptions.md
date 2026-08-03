---
title: "IdentityServer Schema Ownership Exceptions"
date: 2026-07-31
category: architecture-patterns
module: IdentityServer
problem_type: architecture
component: data_access
severity: high
applies_when:
  - "Changing IdentityServer EF entities, configurations, or migrations"
  - "Reviewing schema-per-module ownership for legacy IdentityServer tables"
tags: [identityserver, ef-core, schema-ownership, audit, geolocation]
status: current
---

# IdentityServer Schema Ownership Exceptions

IdentityServer normally owns tables in the `Identity` schema. The following legacy schema names remain intentional ownership exceptions in the shared PostgreSQL database:

- `Application.Application` is the IdentityServer `Tenant` aggregate and is written only through IdentityServer tenant workflows.
- `Audit.AuthorizationLog` is the IdentityServer authentication-attempt log. IdentityServer owns its writes and lifecycle even though the schema name is generic.
- `GeoLocation.AddressCountry`, `AddressRegion`, `AddressProvince`, `AddressCity`, and `AddressBarangay` are IdentityServer-owned address reference data used by Identity address workflows.
- `Registry.RegistryConfiguration`, `RegistryConfigurationGroup`, and `RegistryFavoriteType` are IdentityServer-owned tenant configuration and favorite classification records.
- `Affiliate.Subscription` and `SubscriptionType` are legacy IdentityServer-owned tenant subscription records.

These schema names are retained for migration and deployment compatibility. They do not grant other modules permission to mutate the tables directly. Other modules must use an approved IdentityServer wrapper or deliberate read model, and new IdentityServer entities should use the `Identity` schema unless a separate architecture decision documents another exception.

Moving these tables is a separate compatibility migration requiring coordinated foreign-key, generated endpoint, Portal, and deployment analysis. Normal feature work must not silently remap them.

## Storage Transition Migration

Migration `20260730170511_IdentityServerBackendHardening` is a one-time coordinated exception in the shared migration path. It deduplicates legacy Storage avatar metadata and adds the Storage uniqueness indexes required before credential avatars can move to Storage-owned wrapper operations. The migration runner may update both schemas during that upgrade because it is the repository's single schema authority; IdentityServer runtime code must not write Storage tables directly. All ongoing avatar reads and mutations use the Storage service wrapper.
