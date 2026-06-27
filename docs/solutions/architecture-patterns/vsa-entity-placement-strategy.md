---
title: "VSA Entity Placement Strategy for Module Migration"
date: 2025-11-25
category: architecture-patterns
module: XFramework
problem_type: architecture_pattern
component: development_workflow
severity: high
applies_when:
  - "Deciding where generated VSA entity wrappers, pure Domain.Shared entities, mappings, services, and endpoints belong during module migration"
tags: [vsa, architecture, source-generators, clean-architecture, modules]
---

# ADR-003: VSA Entity Placement Strategy for Module Migration

## Status

Proposed - Awaiting approval and implementation.

## Context

We have successfully completed Phase 2.1 (Source Generator Proof of Concept) and Phase 2.2 (CQRS Framework Removal). The EntityServiceGenerator and EntityEndpointGenerator are working correctly with test entities (TestProduct, TestCategory, TestSupplier) located in Inventario.Api/Entities/.

## Problem

Production entities across all modules are currently located in Domain.Shared projects. These projects are lightweight shared libraries with minimal dependencies, but the source generators require heavier dependencies that conflict with Domain.Shared's clean architecture principles.

Affected modules include Inventario, Wallets, IdentityServer, Community, Communications, SmsGateway, Payments, and StreamFlow.

## Constraints

- Clean Architecture: Domain.Shared must remain lightweight and infrastructure-agnostic.
- Backward compatibility: Existing API contracts must not break.
- Cross-module communication: Service wrappers rely on Domain.Shared entities.
- Minimal duplication: Avoid maintaining duplicate entity definitions where possible.
- VSA benefits: Preserve source generator advantages.

## Decision

Adopt the VSA Wrapper Entity Pattern:

- Keep Domain.Shared entities as pure domain models.
- Create VSA-specific entity wrappers in `Module.Api/Entities/`.
- Implement bidirectional mapping between Domain.Shared and VSA entities.
- Generate services and endpoints from VSA entities.
- Convert at API boundaries to maintain existing contracts.

## Rationale

### Option 1: Add Dependencies to Domain.Shared - Rejected

Adding EF Core, ASP.NET Core, and XFramework.Core to Domain.Shared projects would break clean architecture, create heavy dependencies, increase coupling, violate single-responsibility boundaries, and make testing harder.

### Option 2: VSA Wrapper Entities - Selected

Create lightweight VSA entity wrappers in Module.Api/Entities/ that map to Domain.Shared entities. This preserves clean architecture, keeps dependencies separated, supports backward compatibility, remains testable, and gives the source generators the metadata they need.

### Option 3: Partial Classes Across Projects - Rejected

Using partial classes to extend Domain.Shared entities from Api projects is technically infeasible and architecturally unsound.

### Option 4: Split Generation - Deferred

Generating services in Core projects and endpoints in Api projects separately adds complexity without enough benefit for the current migration.

## Implementation Details

Use an `Entity` suffix for VSA entities to distinguish them from domain entities.

Recommended structure:

```text
Domain.Shared/     pure domain entities and contracts
Core/              existing custom services when still needed
Api/Entities/      VSA wrapper entities, mappings, GenerateEndpoints attributes
Api/Features/      generated or manual VSA endpoint slices
```

Dependency flow:

```text
Domain.Shared -> Core -> Api/Entities -> generated services/endpoints
```

## Migration Strategy

- Phase 1: Pilot with Inventario. Create ProductEntity, apply attributes, implement mappings, verify generation, test endpoints, and document learnings.
- Phase 2: Apply the established pattern to standard modules such as Wallets and Communications.
- Phase 3: Handle complex modules such as IdentityServer and Community with relationship-heavy models.
- Phase 4: Remove old manual CRUD code, update documentation, and monitor performance.

## Consequences

Positive:
- Clean architecture is maintained.
- VSA and source-generation benefits are preserved.
- API contracts remain backward compatible.
- Migration can proceed gradually.

Negative:
- Some property duplication is unavoidable.
- Mapping code must be maintained.
- The pattern adds initial migration effort across many entities.

## Success Metrics

- 100% of production entities migrated to the VSA pattern.
- 60% or greater reduction in manual CRUD code.
- No significant source-generation build-time regression.
- No API response-time regression.
- Reduced developer friction around CRUD boilerplate.

## Related

- [GenerateEndpoints Attribute Usage Guide](../tooling-decisions/generate-endpoints-attribute-usage.md)
- [Generated Endpoint Auto-Discovery and Registration](../tooling-decisions/generated-endpoint-auto-discovery.md)
- [Migration to Auto-Discovery Guide](../developer-experience/migration-to-auto-discovery.md)
- [VSA Entity Migration Guide](../conventions/vsa-entity-migration-guide.md)
