# XFramework Development Roadmap

> **Last Updated:** 2025-12-03
> **Status:** ✅ Phase 4 Performance Optimization COMPLETE

---

## Overview

This roadmap outlines the phased approach to modernizing the XFramework codebase, focusing on:
1. Migrating from CQRS/MediatR to **Feature-Centric Vertical Slice Architecture (VSA)**
2. Implementing unified response patterns (Result<T>)
3. Optimizing database operations (EF Core)
4. Adding hybrid caching layer (L1+L2)
5. Standardizing logging and observability (OpenTelemetry)

---

## Phase 1: Foundation (✅ COMPLETE - 100%)

### Result<T> Pattern
- [x] Create base `Result<T>` record with Success, Failure, NotFound, Unauthorized, Forbidden, Conflict, ValidationError methods
- [x] Create non-generic `Result` record for void operations
- [x] Implement HTTP status code mapping via StatusCode property
- [x] Add implicit operators for common conversions

**Location:** [`src/Kernel/XFramework.Core/Patterns/Result.cs`](src/Kernel/XFramework.Core/Patterns/Result.cs)

### Unit Tests
- [x] Comprehensive Result pattern tests (60+ test cases)

**Location:** [`src/Tests/XFramework.Core.Tests/Patterns/ResultTests.cs`](src/Tests/XFramework.Core.Tests/Patterns/ResultTests.cs) (806 lines)

---

## Phase 2: Core Infrastructure (✅ COMPLETE - 100%)

### EF Core Optimizations
- [x] Configure global query filters for soft delete (`ISoftDeletable`)
- [x] Configure global query filters for multi-tenancy (`IHasTenantId`)
- [x] Configure `AuditInterceptor` for `IAuditable` entities
- [x] Add audit timestamp handling in `OnBeforeSaveChanges`

**Locations:**
- [`src/Kernel/XFramework.Domain/Contexts/XDbContext.cs`](src/Kernel/XFramework.Domain/Contexts/XDbContext.cs)
- [`src/Kernel/XFramework.Domain/Interceptors/AuditInterceptor.cs`](src/Kernel/XFramework.Domain/Interceptors/AuditInterceptor.cs)

### Hybrid Caching (L1 + L2)
- [x] Design `ICacheService` interface with full API
- [x] Implement `HybridCacheService` with Memory (L1) + Redis (L2)
- [x] Add `CacheOptions` configuration class
- [x] Implement graceful degradation when L2 unavailable
- [x] Implement cache statistics tracking
- [x] Add `GetOrSetAsync`, `RemoveByPrefixAsync`, `ClearAllAsync` methods
- [x] Comprehensive unit tests (1084 lines, 40+ test cases)

**Locations:**
- [`src/Kernel/XFramework.Core/Services/Caching/ICacheService.cs`](src/Kernel/XFramework.Core/Services/Caching/ICacheService.cs)
- [`src/Kernel/XFramework.Core/Services/Caching/HybridCacheService.cs`](src/Kernel/XFramework.Core/Services/Caching/HybridCacheService.cs)
- [`src/Tests/XFramework.Core.Tests/Services/Caching/HybridCacheServiceTests.cs`](src/Tests/XFramework.Core.Tests/Services/Caching/HybridCacheServiceTests.cs)

### Logging Standards
- [x] Create structured logging extensions (`LoggerExtensions.cs`)
- [x] Define logging standards documentation

**Locations:**
- [`src/Kernel/XFramework.Core/Loggers/LoggerExtensions.cs`](src/Kernel/XFramework.Core/Loggers/LoggerExtensions.cs)
- [`docs/standards/logging-standards.md`](docs/standards/logging-standards.md)

### Source Generators
- [x] Implement `EntityEndpointGenerator` for auto-generating REST endpoints
- [x] Support `[GenerateEndpoints]` attribute with CRUD configuration

**Location:** [`src/SourceGenerators/XFramework.SourceGenerators/EntityEndpointGenerator.cs`](src/SourceGenerators/XFramework.SourceGenerators/EntityEndpointGenerator.cs)

---

## Phase 3: Feature-Centric VSA Migration (✅ COMPLETE - 100%)

### Feature-Centric VSA Pattern

The target architecture for each feature (from Improvement Plan):
```
Features/
├── Products/
│   ├── ProductEndpoints.cs       # Groups all product endpoint mappings
│   ├── ProductResponse.cs        # Shared response DTO
│   ├── Create/
│   │   ├── Endpoint.cs           # Minimal API endpoint
│   │   └── CreateProductValidator.cs
│   ├── Get/
│   │   └── Endpoint.cs
│   ├── GetList/
│   │   └── Endpoint.cs
│   ├── Update/
│   │   ├── Endpoint.cs
│   │   └── UpdateProductValidator.cs
│   ├── Delete/
│   │   └── Endpoint.cs
│   └── Shared/                   # Shared utilities for this feature
```

### Module Migration Status

| Module | Feature-Centric VSA | Service Layer | Endpoints Status | Legacy Code | slnx Status |
|--------|---------------------|---------------|------------------|-------------|-------------|
| **Inventario** | ✅ COMPLETE | ✅ ProductService | ✅ All CRUD endpoints | ❌ Removed | ✅ Clean |
| **Wallets** | ✅ COMPLETE | ✅ WalletService | ✅ 8 endpoints implemented | ❌ Removed | ✅ Clean |
| **IdentityServer** | ✅ COMPLETE | ✅ AuthService | ✅ 9 endpoints implemented | ❌ Removed | ✅ Clean |
| **Community** | ✅ COMPLETE | ✅ CommunityService | ✅ 3 endpoints implemented | ❌ Removed | ✅ Clean |
| **Messaging** | ✅ COMPLETE | ✅ MessagingService | ✅ 2 endpoints implemented | ❌ Removed | ✅ Clean |
| **SmsGateway** | ✅ COMPLETE | ✅ SmsService | ✅ 6 endpoints implemented | ❌ Removed | ✅ Clean |
| **StreamFlow** | ✅ COMPLETE | ✅ StreamFlowService | ✅ SignalR Hub (appropriate) | ❌ Removed | ✅ Clean |
| **Payments** | ⏸️ DEFERRED | ❓ Integration only | ❌ None | ⚠️ Integration only | N/A |
| **Coins** | ✅ COMPLETE | ✅ BlockchainService | ✅ 1 endpoint modernized | ❌ Removed | ✅ Standalone |

### Detailed Module Assessment

#### ✅ Inventario (COMPLETE - Reference Implementation)
- **Features folder:** ✅ Complete with all endpoint files
- **ProductEndpoints.cs:** Maps Create, Get, GetList, Update, Delete
- **Each operation:** Has dedicated `Endpoint.cs` file using Minimal API pattern
- **Validators:** FluentValidation integrated
- **Service Layer:** ProductService using Result<T> pattern
- **Source Generator:** Commented out (replaced with manual VSA)

#### ✅ Wallets (COMPLETE - 2025-11-26)
- **Features folder:** ✅ Complete with all endpoint files
- **Endpoints implemented:**
  - `POST /api/wallets` - Create wallet
  - `GET /api/wallets/{walletId}` - Get wallet by ID
  - `GET /api/wallets/credential/{credentialId}` - Get wallets by credential
  - `POST /api/wallets/add-funds` - Add funds (increment)
  - `POST /api/wallets/withdraw-funds` - Withdraw funds (decrement)
  - `POST /api/wallets/transfer` - Transfer between wallets
  - `POST /api/wallets/convert` - Convert currency
  - `POST /api/wallets/release-transaction` - Release held transaction
- **Validators:** ✅ 5 FluentValidation validators created
- **Service Layer:** ✅ WalletService (1069 lines, uses Result<T>)
- **Source Generator:** Commented out (replaced with manual VSA)
- **Legacy Code Cleanup:** ✅ Complete
  - Wallets.Core/ and Wallets.Integration/ removed
  - Solution file updated (removed stale project references)
  - Orphaned EntityService partial methods removed from mapping files
  - Missing System.Net namespace added to WalletService.cs
- **Build Status:** ✅ 0 errors, 43 pre-existing warnings

#### ✅ IdentityServer (COMPLETE - 2025-11-26)
- **Features folder:** ✅ Complete with all endpoint files
- **Endpoints implemented:**
  - `POST /api/auth/authenticate` - Multi-type authentication
  - `POST /api/auth/change-password` - Change password
  - `POST /api/auth/verify-password` - Verify password
  - `POST /api/credentials` - Create credential
  - `PATCH /api/credentials/{id}` - Update credential
  - `POST /api/verifications` - Create SMS OTP
  - `PATCH /api/verifications/{token}` - Confirm verification
  - `GET /api/verifications/check` - Check verification status
  - `POST /api/files` - Upload file to Azure Blob Storage
- **Validators:** ✅ 7 FluentValidation validators created
- **Service Layer:** ✅ AuthService (936 lines, uses Result<T>)
- **Source Generator:** Commented out (replaced with manual VSA)
- **Legacy Code Cleanup:** ✅ Complete
  - IdentityServer.Core/ and IdentityServer.Integration/ removed
  - Solution files updated (XFramework.slnx, XFramework.IdentityServer.sln)
  - XFramework.Core.csproj updated to remove Integration reference
- **Build Status:** ✅ 0 errors, 210 pre-existing warnings

#### ✅ Community (COMPLETE - 2025-11-27)
- **Features folder:** ✅ Complete with all endpoint files
- **Endpoints implemented:**
  - `POST /api/community/connection` - Create community connection
  - `GET /api/community/connection/{connectionId}` - Get connection by ID
  - `GET /api/community/connection/identity/{identityId}` - Get connections by identity
- **Validators:** ✅ 2 FluentValidation validators created
- **Service Layer:** ✅ CommunityService (uses Result<T>)
- **Legacy Code Cleanup:** ✅ Complete
  - Community.Core/ and Community.Integration/ removed
  - Solution file updated
- **Build Status:** ✅ 0 errors

#### ✅ Messaging (COMPLETE - 2025-11-27)
- **Features folder:** ✅ Complete with all endpoint files
- **Endpoints implemented:**
  - `POST /api/messages` - Send message
  - `GET /api/messages/thread/{threadId}` - Get message thread
- **Validators:** ✅ 2 FluentValidation validators created
- **Service Layer:** ✅ MessagingService (uses Result<T>)
- **Legacy Code Cleanup:** ✅ Complete
  - Messaging.Core/ removed
  - Messaging.Integration/ kept (external consumers)
  - Solution file updated
- **Build Status:** ✅ 0 errors

#### ✅ SmsGateway (COMPLETE - 2025-11-27)
- **Features folder:** ✅ Complete with all endpoint files
- **Endpoints implemented:**
  - `POST /api/sms` - Send SMS message
  - `POST /api/sms/bulk` - Send bulk SMS
  - `GET /api/sms/{messageId}` - Get SMS status
  - `GET /api/sms/history` - Get SMS history
  - `GET /api/SmsGatewayNode/List/{agentClusterId}` - List SMS nodes (legacy route preserved)
  - `POST /api/sms/receive` - Receive incoming SMS webhook
- **Validators:** ✅ 2 FluentValidation validators created
- **Service Layer:** ✅ SmsService (uses Result<T>)
- **Legacy Code Cleanup:** ✅ Complete
  - SmsGateway.Core/ removed
  - SmsGateway.Integration/ kept (external consumers)
  - Legacy SmsGatewayNodeController removed
  - Solution file updated
- **Build Status:** ✅ 0 errors, 179 warnings

#### ✅ StreamFlow (COMPLETE - 2025-11-27)
- **Architecture:** SignalR Hub retained (appropriate for real-time messaging)
- **Service Consolidation:** ✅ Core services moved to Stream project
  - ICachingService, CachingService
  - IStreamFlowService, StreamFlowService
  - StreamFlowMessageQueue (Channel-based)
  - StreamFlowProcessor (BackgroundService)
- **Legacy Code Cleanup:** ✅ Complete
  - StreamFlow.Core/ removed (consolidated into Stream)
  - StreamFlow.Domain.Shared/ kept (external contracts)
  - Solution file updated
- **Build Status:** ✅ 0 errors, 136 warnings

#### ✅ Coins (COMPLETE - 2025-11-27)
- **Full Modernization:** Complete rewrite from legacy to modern patterns
- **Features folder:** ✅ Complete with endpoint files
- **Endpoints implemented:**
  - `POST /Blockchain/Send` - Bulk send Bitcoin transactions
- **Validators:** ✅ SendTransactionsValidator created
- **Service Consolidation:** ✅ Complete
  - BlockchainService, CachingService moved to Api
  - BusinessObjects, Configurations moved to Api
  - BlockchainAPI.dll preserved in Libraries
- **Legacy Code Cleanup:** ✅ Complete
  - Coins.Core/ removed
  - Coins.Domain/ removed
  - Legacy Startup.cs removed (modern minimal hosting)
  - Legacy Controllers removed
  - Solution file updated (standalone XFramework.Coins.sln)
- **Build Status:** ✅ 0 errors, 3/3 tests passed

#### ⏸️ Payments (DEFERRED)
- **Status:** Integration-only module (no API endpoints)
- **Contains:** Payment gateway wrapper only
- **Action:** Deferred - no VSA migration needed

---

## Phase 4: Performance Optimization (✅ COMPLETE - 100%)

### EF Core Global Optimizations (DbInstaller.cs across all modules)
- [x] `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` - Global default
- [x] `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` - Global default
- [x] Npgsql connection pooling - Enabled by default

### Service-Level Query Optimizations
- [x] WalletService.cs - 5 explicit `AsNoTracking()` additions for read-only queries
  - CreateWalletAsync: WalletType lookup (line 58)
  - GetWalletAsync: Read-only query (line 119)
  - GetWalletsByCredentialAsync: Read-only query (line 148)
  - TransferAsync: Config lookup (line 552)
  - ConvertWalletAsync: Config lookup (line 836)
- [x] ProductService.cs - `AsNoTracking()` + `AsSplitQuery()` on read operations
  - GetByIdAsync: With Include(Category) (lines 127-128)
  - GetListAsync: With Include(Category) (lines 166-167)

### Existing Optimizations (Already Implemented)
- [x] Channel-based message queueing in StreamFlowService
- [x] OpenTelemetry integration in WalletService (tracing/metrics)
- [x] Hybrid caching infrastructure (L1 Memory + L2 Redis)
- [x] Cached JsonSerializerOptions in HybridCacheService and HelperService

### Not Needed (Analysis Complete)
- [x] Memory pooling - Not required (JsonSerializerOptions already cached, allocations are modest)
- [x] Connection pooling - Already optimized via Npgsql defaults

---

## Phase 5: Documentation & Cleanup (🔄 IN PROGRESS - ~60%)

### Documentation
- [x] VSA migration guide
- [x] Logging standards documentation
- [x] OpenTelemetry guide
- [x] Source generator usage documentation
- [ ] Update API documentation
- [ ] Create architecture decision records (ADRs)
- [ ] Document Feature-Centric VSA pattern (reference Inventario)

### Code Cleanup
- [ ] Remove deprecated CQRS handlers from migrated modules
- [ ] Clean up unused MediatR registrations
- [ ] Remove legacy validation classes
- [ ] Remove empty Features folders (Wallets)
- [ ] Delete commented-out source generator attributes after migration

**Documentation Locations:**
- [`docs/migration/vsa-entity-migration-guide.md`](docs/migration/vsa-entity-migration-guide.md)
- [`docs/standards/logging-standards.md`](docs/standards/logging-standards.md)
- [`docs/observability/opentelemetry-guide.md`](docs/observability/opentelemetry-guide.md)
- [`docs/source-generators/`](docs/source-generators/)

---

## Summary Progress

| Phase | Description | Progress | Status |
|-------|-------------|----------|--------|
| **Phase 1** | Foundation (Result<T>, Tests) | 100% | ✅ Complete |
| **Phase 2** | Core Infrastructure (EF, Caching, Logging) | 100% | ✅ Complete |
| **Phase 3** | Feature-Centric VSA Migration | 100% | ✅ Complete |
| **Phase 4** | Performance Optimization | 100% | ✅ Complete |
| **Phase 5** | Documentation & Cleanup | ~60% | 🔄 In Progress |

**Overall Progress: ~92%**

---

## Key Findings from Codebase Review

### What's Working Well
1. **Result<T> Pattern** - Fully implemented in XFramework.Core
2. **Hybrid Caching** - Complete L1+L2 implementation with comprehensive tests
3. **Service Layer** - All modules now use service classes with Result<T>
4. **Feature-Centric VSA** - ✅ All modules migrated (Inventario as reference)
5. **Legacy Cleanup** - ✅ All Core/Integration projects removed (except where needed for external consumers)

### Completed Migration Summary (2025-11-27)
| Module | Endpoints | Validators | Build Status |
|--------|-----------|------------|--------------|
| Inventario | 5 CRUD | Multiple | ✅ |
| Wallets | 8 | 5 | ✅ 0 errors |
| IdentityServer | 9 | 7 | ✅ 0 errors |
| Community | 3 | 2 | ✅ 0 errors |
| Messaging | 2 | 2 | ✅ 0 errors |
| SmsGateway | 6 | 2 | ✅ 0 errors |
| StreamFlow | SignalR Hub | N/A | ✅ 0 errors |
| Coins | 1 | 1 | ✅ 0 errors |

### Remaining Work
1. ~~**Performance Optimization**~~ ✅ Complete (2025-12-03)
2. **Documentation** - Complete ADRs and API documentation
3. **Testing** - Add comprehensive integration tests for new endpoints

---

## Next Steps (Priority Order)

### ✅ Completed (Phase 3 VSA Migration)
1. ~~**Complete Wallets Feature Endpoints**~~ ✅ DONE (2025-11-26)
2. ~~**Migrate IdentityServer**~~ ✅ DONE (2025-11-26) - 9 endpoints, full cleanup
3. ~~**Migrate Community**~~ ✅ DONE (2025-11-27) - 3 endpoints, full cleanup
4. ~~**Migrate Messaging**~~ ✅ DONE (2025-11-27) - 2 endpoints, full cleanup
5. ~~**Migrate SmsGateway**~~ ✅ DONE (2025-11-27) - 6 endpoints, legacy controller removed
6. ~~**Migrate StreamFlow**~~ ✅ DONE (2025-11-27) - Service consolidation, SignalR kept
7. ~~**Migrate Coins**~~ ✅ DONE (2025-11-27) - Full modernization, standalone solution

### Short-term (Phase 5 - Documentation)
8. ~~**Performance Optimization**~~ ✅ Complete - Global defaults + explicit additions
9. ~~**Memory Pooling**~~ ✅ Analyzed - Not needed (modest scale)
10. ~~**Connection Pooling**~~ ✅ Optimized - Npgsql defaults

### Medium-term
11. **Integration Tests** - Add comprehensive tests for new endpoints
12. **API Documentation** - Update OpenAPI/Swagger documentation
13. **Architecture Decision Records** - Document key decisions

### Long-term
14. **Monitoring & Alerting** - Enhance OpenTelemetry dashboards
15. **Security Audit** - Review authentication/authorization patterns
16. **Performance Benchmarks** - Establish baseline metrics

---

## Migration Template (From Inventario)

When migrating a module, follow this pattern from [`src/Modules/XFramework.Inventario`](src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/):

### Phase A: Create Feature Endpoints
1. **Create Features folder** in `[Module].Api/Features/`
2. **Create Feature folder** for each domain entity (e.g., `Products/`)
3. **Create `[Feature]Endpoints.cs`** to aggregate all operation mappings
4. **Create `[Feature]Response.cs`** for shared response DTO
5. **Create operation folders** (Create/, Get/, GetList/, Update/, Delete/)
6. **Create `Endpoint.cs`** in each operation folder using Minimal API
7. **Create validators** (e.g., `CreateProductValidator.cs`) using FluentValidation
8. **Register endpoints** in `Program.cs` using `MapProductEndpoints()`
9. **Comment out/remove** source generator attributes

### Phase B: Legacy Cleanup (Required for Complete Migration)
10. **Move services** from `[Module].Core/` to `[Module].Api/Services/`
11. **Delete legacy projects** ([Module].Core/, [Module].Integration/)
12. **Update solution file** to remove stale project references
13. **Fix orphaned partial methods** in Entity mapping files (remove EntityService stubs)
14. **Delete legacy handlers** (Commands/, Query/)
15. **Verify build** - must compile with 0 errors

---

## References

- [XFramework Improvement Plan](XFramework-Improvement-Plan.md)
- [AI Development Guide](AI-DEVELOPMENT-GUIDE.md)
- [VSA Migration Guide](docs/migration/vsa-entity-migration-guide.md)
- [Logging Standards](docs/standards/logging-standards.md)
- [OpenTelemetry Guide](docs/observability/opentelemetry-guide.md)
- **Reference Implementation:** [`src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/`](src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/)