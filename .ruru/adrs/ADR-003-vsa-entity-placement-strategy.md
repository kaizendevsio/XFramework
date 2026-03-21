+++
id = "ADR-003"
title = "VSA Entity Placement Strategy for Module Migration"
status = "proposed"
date = "2025-11-25"
context_type = "decision"
decision_type = "architecture"
scope = "XFramework VSA Migration"
tags = ["vsa", "architecture", "source-generators", "clean-architecture", "modules"]
+++

# ADR-003: VSA Entity Placement Strategy for Module Migration

## Status

**Proposed** - Awaiting approval and implementation

## Context

### Background

We have successfully completed Phase 2.1 (Source Generator Proof of Concept) and Phase 2.2 (CQRS Framework Removal). The EntityServiceGenerator and EntityEndpointGenerator are working correctly with test entities (TestProduct, TestCategory, TestSupplier) located in Inventario.Api/Entities/.

### The Problem

Production entities across all modules are currently located in Domain.Shared projects. These projects are lightweight shared libraries with minimal dependencies but the source generators require heavy dependencies that conflict with Domain.Shared's clean architecture principles.

### Modules Affected

Total: 30+ entities requiring migration across Inventario, Wallets, IdentityServer, Community, Messaging, SmsGateway, Payments, and StreamFlow modules.

### Constraints

1. Clean Architecture: Domain.Shared must remain lightweight and infrastructure-agnostic
2. Backward Compatibility: Existing API contracts must not break
3. Cross-Module Communication: ServiceWrappers rely on Domain.Shared entities
4. Minimal Duplication: Avoid maintaining duplicate entity definitions
5. VSA Benefits: Maintain source generator advantages

## Decision

We will adopt the VSA Wrapper Entity Pattern where Domain.Shared entities remain pure domain models, we create VSA-specific entity wrappers in Module.Api/Entities/, implement bidirectional mapping between Domain and VSA entities, generate services and endpoints using VSA entities, and convert at API boundaries to maintain existing contracts.

## Rationale

### Options Considered

#### Option 1: Add Dependencies to Domain.Shared - REJECTED

Adding EF Core, ASP.NET Core, and XFramework.Core to Domain.Shared projects would break clean architecture, create heavy dependencies, increase coupling, violate SRP, and make testing difficult.

#### Option 2: VSA Wrapper Entities - SELECTED

Create lightweight VSA entity wrappers in Module.Api/Entities/ that map to Domain.Shared entities. This preserves clean architecture, maintains separation of concerns, provides flexibility per module, ensures backward compatibility, maintains testability, and is generator-friendly.

#### Option 3: Partial Classes Across Projects - REJECTED

Using partial classes to extend Domain.Shared entities from Api projects is technically infeasible and architecturally unsound.

#### Option 4: Split Generation - DEFERRED

Generating services in Core projects and endpoints in Api projects separately adds unnecessary complexity for no clear benefit.

## Implementation Details

### Naming Convention

Use "Entity" suffix for VSA entities to clearly distinguish from domain entities.

### File Structure

Domain.Shared contains pure domain entities (unchanged), Core contains existing custom services, and Api/Entities contains VSA entities with GenerateEndpoints attribute, mappings, and all required dependencies.

### Dependency Flow

Domain.Shared (pure) is referenced by Core (optional custom services), which is referenced by Api/Entities (VSA layer with generators), which generates services and endpoints.

## Migration Strategy

Phase 1: Pilot Module (Inventario) - Create ProductEntity, apply attribute, implement mappings, verify generation, test endpoints, document learnings.

Phase 2: Standard Modules - Follow established pattern for Wallets, Messaging, etc.

Phase 3: Complex Modules - Handle IdentityServer and Community with complex relationships.

Phase 4: Cleanup - Remove old manual CRUD code, update documentation, train team, monitor performance.

## Consequences

### Positive

Clean Architecture Maintained, VSA Benefits Achieved, Backward Compatible, Flexible, Testable, Gradual Migration, Future-Proof.

### Negative

Property Duplication, Mapping Code, Complexity, Initial Effort (30+ entities need VSA wrappers).

### Neutral

Learning Curve, Documentation, Tooling.

## Success Metrics

1. Coverage: 100% of production entities migrated to VSA pattern
2. Code Reduction: 60%+ reduction in manual CRUD code
3. Build Time: No significant increase despite source generation
4. Runtime Performance: No regression in API response times
5. Developer Satisfaction: Positive feedback on reduced boilerplate

## Related Documents

- Attribute Usage Guide
- Auto-Discovery Guide
- Migration to Auto-Discovery
- Phase 2 Journal