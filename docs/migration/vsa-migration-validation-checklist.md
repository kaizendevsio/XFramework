# VSA Migration Validation Checklist

## Overview

This checklist ensures complete and correct migration of entities to the VSA (Vertical Slice Architecture) pattern. Use this for each entity and module migration.

**Related Documents:**
- [VSA Entity Migration Guide](./vsa-entity-migration-guide.md)
- [ADR-003: VSA Entity Placement Strategy](../../.ruru/adrs/ADR-003-vsa-entity-placement-strategy.md)

## Pre-Migration Requirements

Before starting any migration, verify:

### Environment Setup
- [ ] .NET 10.0 SDK installed
- [ ] XFramework.SourceGenerators project compiles successfully
- [ ] Test entities (TestProduct, TestCategory, TestSupplier) work correctly
- [ ] Auto-discovery extensions available in XFramework.Core

### Documentation Review
- [ ] Read ADR-003 completely
- [ ] Understand VSA Wrapper Entity Pattern
- [ ] Review working example: TestProduct.cs and TestProduct.Mappings.cs
- [ ] Understand difference between Domain and VSA entities

### Module Analysis
- [ ] List all Domain.Shared entities in the module
- [ ] Categorize entities by complexity (Basic, Standard, Complex)
- [ ] Identify entities with existing custom services
- [ ] Document entities with complex business logic
- [ ] Identify read-only entities (audit logs, transactions)

## Per-Entity Migration Checklist

Use this checklist for **each entity** being migrated.

### Entity Information

**Entity Name:** ___________________  
**Module:** ___________________  
**Complexity:** [ ] Basic  [ ] Standard  [ ] Complex  
**Date Started:** ___________________  
**Migrated By:** ___________________  

### Phase 1: Analysis

- [ ] Located domain entity in Domain.Shared
- [ ] Documented all properties and their types
- [ ] Identified navigation properties and relationships
- [ ] Reviewed existing custom services (if any)
- [ ] Determined required CRUD operations
- [ ] Identified business logic that must be preserved
- [ ] Reviewed existing API contracts
- [ ] Checked for ServiceWrapper dependencies

### Phase 2: VSA Entity Creation

#### File: `{Entity}Entity.cs`

- [ ] Created file in `{Module}.Api/Entities/` directory
- [ ] Named entity with "Entity" suffix (e.g., `ProductEntity`)
- [ ] Applied `[GenerateEndpoints]` attribute
- [ ] Configured `Type` parameter correctly
  - [ ] `Both` for full generation
  - [ ] `Service` for service only
  - [ ] `Rest` for endpoints only
- [ ] Configured `Actions` parameter correctly
  - [ ] `All` for full CRUD
  - [ ] `ReadOnly` for GET operations only
  - [ ] Custom combination if needed
- [ ] Set `RoutePrefix` (e.g., "api/products")
- [ ] Set `RequireAuthorization` appropriately
- [ ] Set `Roles` if role-based access needed
- [ ] Set `CacheDurationSeconds` based on data volatility
- [ ] Set `CacheKeyPrefix` if custom cache keys needed
- [ ] Made class `partial`
- [ ] Added `Id` property (Guid)
- [ ] Copied all required properties from domain entity
- [ ] Flattened navigation properties to IDs where appropriate
- [ ] Added XML documentation comments

#### Request DTOs

- [ ] Created `Create{Entity}EntityRequest` class
- [ ] Included all properties needed for creation
- [ ] Added validation attributes if needed
- [ ] Created `Update{Entity}EntityRequest` class
- [ ] Included all properties that can be updated
- [ ] Added validation attributes if needed
- [ ] Created `Get{Entity}EntityListRequest` class
- [ ] Included `Page` property (default: 1)
- [ ] Included `PageSize` property (default: 20)
- [ ] Added filter properties as needed
- [ ] Added XML documentation for all DTOs

### Phase 3: Mapping Implementation

#### File: `{Entity}Entity.Mappings.cs`

- [ ] Created mappings file in same directory
- [ ] Created `{Entity}EntityMappings` static class
- [ ] Implemented `ToVsaEntity(this Domain.Entity domain)` extension
- [ ] Mapped all properties Domain → VSA
- [ ] Handled null values appropriately
- [ ] Implemented `ToDomainEntity(this {Entity}Entity vsa)` extension
- [ ] Mapped all properties VSA → Domain
- [ ] Handled null values appropriately
- [ ] Created partial class `{Entity}EntityService`
- [ ] Implemented `MapCreateRequestToEntity` method
- [ ] Created domain entity from request
- [ ] Converted to VSA entity using mapping
- [ ] Implemented `MapUpdateRequestToEntity` method
- [ ] Updated entity properties from request
- [ ] Set `UpdatedAt` timestamp
- [ ] Implemented `ApplyFilters` method
- [ ] Added search/filter logic as needed
- [ ] Added sorting logic
- [ ] Added XML documentation for mapping methods

### Phase 4: Build Verification

- [ ] Project builds without errors
- [ ] Project builds without warnings (critical ones)
- [ ] Generated files appear in `obj/Debug/net10.0/generated/`
- [ ] Found `{Entity}EntityService.g.cs`
- [ ] Found `{Entity}EntityEndpoints.g.cs`
- [ ] Opened generated service file - no compilation errors
- [ ] Opened generated endpoint file - no compilation errors
- [ ] Verified partial methods match signatures

### Phase 5: Service Registration

- [ ] Verified `AddGeneratedServices()` in Program.cs
- [ ] Ran application in debug mode
- [ ] Checked startup logs for service registration
- [ ] Confirmed `I{Entity}EntityService` registered
- [ ] Confirmed service lifetime is correct (typically Scoped)
- [ ] No duplicate registration warnings
- [ ] Service can be injected in controllers/endpoints

### Phase 6: Endpoint Verification

- [ ] Verified `MapGeneratedEndpoints()` in Program.cs
- [ ] Application starts without errors
- [ ] Navigated to `/swagger`
- [ ] Found `{Entity}` tag in Swagger UI
- [ ] Verified all expected endpoints present:
  - [ ] GET `{RoutePrefix}/{id}` (if Actions includes Get)
  - [ ] GET `{RoutePrefix}` (if Actions includes GetList)
  - [ ] POST `{RoutePrefix}` (if Actions includes Create)
  - [ ] PUT `{RoutePrefix}/{id}` (if Actions includes Update)
  - [ ] DELETE `{RoutePrefix}/{id}` (if Actions includes Delete)
- [ ] Verified authorization requirements shown correctly
- [ ] Verified request/response schemas shown in Swagger

### Phase 7: Functional Testing

#### GET by ID Endpoint
- [ ] Called GET `{RoutePrefix}/{validId}`
- [ ] Received 200 OK response
- [ ] Response data structure correct
- [ ] All properties populated correctly
- [ ] Called GET `{RoutePrefix}/{invalidId}`
- [ ] Received 404 Not Found response
- [ ] Error message is appropriate

#### GET List Endpoint
- [ ] Called GET `{RoutePrefix}` without parameters
- [ ] Received 200 OK response
- [ ] Response is array of entities
- [ ] Pagination works (Page, PageSize)
- [ ] Called with Page=1, PageSize=10
- [ ] Received correct number of items
- [ ] Filters work correctly (if implemented)
- [ ] Sorting works correctly (if implemented)

#### POST Create Endpoint
- [ ] Called POST `{RoutePrefix}` with valid data
- [ ] Received 201 Created response
- [ ] Response includes created entity with ID
- [ ] Location header present (optional)
- [ ] Entity persisted to database
- [ ] Called POST with invalid data
- [ ] Received 400 Bad Request response
- [ ] Validation errors returned correctly

#### PUT Update Endpoint
- [ ] Called PUT `{RoutePrefix}/{id}` with valid data
- [ ] Received 200 OK response
- [ ] Response includes updated entity
- [ ] Changes persisted to database
- [ ] Called PUT with invalid ID
- [ ] Received 404 Not Found response
- [ ] Called PUT with invalid data
- [ ] Received 400 Bad Request response

#### DELETE Endpoint
- [ ] Called DELETE `{RoutePrefix}/{validId}`
- [ ] Received 204 No Content response
- [ ] Entity removed from database (or soft-deleted)
- [ ] Called DELETE with invalid ID
- [ ] Received 404 Not Found response

### Phase 8: Authorization Testing

- [ ] Called endpoints without authentication
- [ ] Received 401 Unauthorized (if RequireAuthorization=true)
- [ ] Called endpoints with valid token
- [ ] Received successful response
- [ ] Called endpoints with insufficient roles
- [ ] Received 403 Forbidden (if Roles specified)
- [ ] Called endpoints with correct roles
- [ ] Received successful response

### Phase 9: Caching Testing (if enabled)

- [ ] Called GET endpoint twice in succession
- [ ] Second call served from cache (check logs)
- [ ] Cache duration respected
- [ ] Performed Create/Update/Delete operation
- [ ] Cache invalidated correctly
- [ ] Subsequent GET returns fresh data

### Phase 10: Integration Testing

- [ ] Existing integration tests still pass
- [ ] Created new integration test for VSA entity
- [ ] Tested create → read → update → delete flow
- [ ] Tested error scenarios
- [ ] Tested authorization scenarios
- [ ] Verified ServiceWrapper compatibility (if used)
- [ ] Verified cross-module communication works

### Phase 11: Performance Testing

- [ ] Measured GET response time
- [ ] Response time acceptable (< 200ms for simple queries)
- [ ] Measured POST response time
- [ ] Response time acceptable (< 500ms)
- [ ] Compared with previous implementation (if exists)
- [ ] No significant performance regression
- [ ] Identified any performance optimizations needed

### Phase 12: Documentation

- [ ] Updated module README with migration notes
- [ ] Documented any custom logic or decisions
- [ ] Updated API documentation (if separate)
- [ ] Added code comments for complex mappings
- [ ] Documented any known limitations
- [ ] Created issue tickets for future improvements

## Module Completion Checklist

After all entities in a module are migrated:

### Code Cleanup
- [ ] Removed old manual CRUD implementations
- [ ] Removed obsolete request/response DTOs
- [ ] Removed unused service interfaces
- [ ] Cleaned up commented-out code
- [ ] Removed unused imports/usings

### Testing
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] All E2E tests pass
- [ ] Manual smoke testing completed
- [ ] Performance benchmarks within acceptable range

### Documentation
- [ ] Module README updated
- [ ] API documentation updated
- [ ] CHANGELOG.md updated with migration notes
- [ ] Team wiki updated (if applicable)

### Deployment Preparation
- [ ] Database migrations reviewed (if any)
- [ ] Configuration changes documented
- [ ] Backward compatibility verified
- [ ] Rollback plan documented
- [ ] Deployment checklist created

### Communication
- [ ] Team notified of changes
- [ ] Training session scheduled (if needed)
- [ ] Migration notes shared with stakeholders
- [ ] FAQ document created for common questions

## Project-Wide Completion Checklist

After all modules are migrated:

### Architecture
- [ ] All modules follow VSA pattern consistently
- [ ] Domain.Shared remains lightweight and pure
- [ ] No infrastructure dependencies in Domain.Shared
- [ ] Separation of concerns maintained
- [ ] Clean architecture principles upheld

### Code Quality
- [ ] No code duplication beyond necessary mappings
- [ ] Consistent naming conventions across modules
- [ ] All code follows project standards
- [ ] Code coverage maintained or improved
- [ ] Technical debt addressed

### Performance
- [ ] Overall application performance acceptable
- [ ] No memory leaks introduced
- [ ] Caching strategy effective
- [ ] Database query performance optimized
- [ ] Load testing passed

### Documentation
- [ ] All ADRs up to date
- [ ] Migration guide complete and accurate
- [ ] API documentation complete
- [ ] Developer onboarding docs updated
- [ ] Architecture diagrams updated

### Team Readiness
- [ ] Training completed for all developers
- [ ] VSA pattern well understood
- [ ] Best practices documented and shared
- [ ] Code review standards updated
- [ ] Troubleshooting guide available

## Sign-Off

### Entity Migration Sign-Off

**Entity:** ___________________  
**Module:** ___________________  
**Status:** [ ] Complete  [ ] Incomplete  [ ] Blocked  

**Tested By:** ___________________  
**Date:** ___________________  
**Approved By:** ___________________  
**Date:** ___________________  

**Notes:**
```
[Add any notes, issues, or follow-up items]
```

### Module Migration Sign-Off

**Module:** ___________________  
**Entities Migrated:** _____ / _____  
**Status:** [ ] Complete  [ ] Incomplete  [ ] Blocked  

**Lead Developer:** ___________________  
**Date:** ___________________  
**Technical Architect:** ___________________  
**Date:** ___________________  

**Notes:**
```
[Add any notes, issues, or follow-up items]
```

## Metrics

Track these metrics throughout migration:

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Entities Migrated | 30+ | ___ | ___ |
| Code Reduction | 60%+ | ___% | ___ |
| Build Time Increase | <10% | ___% | ___ |
| Test Pass Rate | 100% | ___% | ___ |
| Performance Regression | 0% | ___% | ___ |
| Developer Satisfaction | 8+/10 | ___/10 | ___ |

## Common Issues and Resolutions

### Issue: Generated Files Missing
**Resolution:** Check source generator reference, enable EmitCompilerGeneratedFiles, rebuild

### Issue: Compilation Errors in Generated Code
**Resolution:** Verify attribute application, ensure partial class, check Request DTOs exist

### Issue: Endpoints Not in Swagger
**Resolution:** Verify MapGeneratedEndpoints() called, check endpoint naming pattern

### Issue: Service Injection Fails
**Resolution:** Verify AddGeneratedServices() called before Build(), check interface naming

### Issue: Mapping Errors
**Resolution:** Verify property name matching, handle nulls, check type compatibility

## Getting Help

If blocked or encountering issues:

1. Review [Migration Guide](./vsa-entity-migration-guide.md) troubleshooting section
2. Check [ADR-003](../../.ruru/adrs/ADR-003-vsa-entity-placement-strategy.md) architectural decisions
3. Examine working example: TestProduct.cs
4. Enable diagnostic logging
5. Consult with Technical Architect
6. Create issue ticket with details

---

**Version**: 1.0  
**Last Updated**: 2025-11-25  
**Author**: Technical Architect  
**Review Cycle**: After each module completion