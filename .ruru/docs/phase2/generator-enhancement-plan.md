+++
id = "DOC-GENERATOR-ENHANCEMENT-PLAN-V1"
title = "EntityServiceGenerator Enhancement Plan"
context_type = "documentation"
scope = "Technical specification for enhancing source generator to replace CQRS infrastructure"
target_audience = ["architects", "senior-developers"]
granularity = "detailed"
status = "active"
last_updated = "2025-01-24"
version = "1.0"
tags = ["source-generator", "vsa", "cqrs-replacement", "phase2", "enhancement-plan"]
related_context = [
    "src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs",
    "src/Kernel/XFramework.Core/DataAccess/",
    ".ruru/docs/phase2/cqrs-analysis-results.md"
]
+++

# EntityServiceGenerator Enhancement Plan

## Executive Summary

The [`EntityServiceGenerator.cs`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:1) source generator works as proven by [`TestProduct`](src/Modules/XFramework.Inventario/Inventario.Core/Entities/TestProduct.cs:1), but lacks critical enterprise features present in CQRS handlers. This plan details enhancements to enable production-ready CRUD services via `[GenerateEndpoints]` attribute.

**Goal**: Apply `[GenerateEndpoints]` to any entity and receive complete VSA services with tenant isolation, caching, navigation loading, validation, and audit trails.

---

## 1. Current Generator Capabilities ✅

### What Works Well

**Discovery & Pipeline**:
- Incremental source generator using Roslyn's `IIncrementalGenerator`
- Discovers classes with `[GenerateEndpointsAttribute]`
- Generates only when `Type = Service (1)` or `Both (3)`
- Action filtering via flags (Create=1, Get=2, GetList=4, Update=8, Delete=16)

**Generated Structure**:
- Interface: `ITestProductService` with CRUD methods
- Implementation: `TestProductService` as partial class (extensible)
- Dependencies: `DbContext`, `ICacheService`, `ILogger`
- Pattern: `Result<T>` for consistent error handling

**Current Features**:
- ✅ Soft delete filtering (`!e.IsDeleted`)
- ✅ Cache-aside pattern with configurable duration
- ✅ Property mapping with exclusions
- ✅ AsNoTracking for read operations
- ✅ Structured logging

---

## 2. Critical Gaps vs CQRS Handlers 🔴

### Gap #1: **Tenant Isolation** - SECURITY RISK

**CQRS Handlers** (All handlers):
```csharp
public class GetHandler<TModel>(
    ITenantService tenantService, // ← Injected
    // ...
) {
    var tenant = await tenantService.GetTenant(request.TenantId);
    var entity = await query
        .Where(i => i.TenantId == tenant.Id)  // ← Explicit filter
        .FirstOrDefaultAsync(ct);
}
```

**Current Generator** (NO FILTERING):
```csharp
// ❌ SEVERE VULNERABILITY: No tenant filtering!
var entity = await _dbContext.Set<TestProduct>()
    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
```

**Impact**: Cross-tenant data access possible - users can read/modify other tenants' data.

---

### Gap #2: **Navigation Property Loading** - DATA COMPLETENESS

**CQRS Handlers** ([`GetHandler.cs:57-67`](src/Kernel/XFramework.Core/DataAccess/Query/GetHandler.cs:57-67)):
```csharp
if (request.IncludeNavigations is true) {
    if (request.Includes is not null && request.Includes.Any()) {
        query = request.Includes.Aggregate(query, (current, include) => current.Include(include));
    } else {
        query = IncludeNavigations(query, typeof(TModel), _maxNavigationDepth);
    }
}
query = query.AsSplitQuery();
```

**Current Generator** (NO SUPPORT):
```csharp
// ❌ Navigation properties will be null!
var entity = await _dbContext.Set<TestProduct>()
    .AsNoTracking()
    .FirstOrDefaultAsync(e => e.Id == id, ct);
```

**Impact**: N+1 queries, incomplete API responses, manual loading required.

---

### Gap #3: **Audit Field Management**

**CQRS Handlers** ([`CreateHandler.cs:38-41`](src/Kernel/XFramework.Core/DataAccess/Commands/CreateHandler.cs:38-41)):
```csharp
request.Model.Id = request.Model.Id != Guid.Empty ? request.Model.Id : Guid.NewGuid();
request.Model.CreatedAt = DateTime.UtcNow;
request.Model.TenantId = tenant.Id;
// Also sets: CreatedBy, UpdatedAt, UpdatedBy, ConcurrencyStamp
```

**Current Generator**:
```csharp
// Excludes audit fields from mapping but doesn't set them
var propertyMappings = GeneratePropertyMappings(entity, "request", "entity", 
    excludeId: true, excludeAudit: true  // ← Excluded but NOT set!
);
```

**Status**: Partially handled by `AuditInterceptor`, but should be explicit.

---

### Gap #4: **Navigation Property Stripping**

**CQRS Handlers** ([`CreateHandler.cs:44-51`](src/Kernel/XFramework.Core/DataAccess/Commands/CreateHandler.cs:44-51)):
```csharp
var navigationProperties = request.Model.GetType().GetProperties()
    .Where(p => IsNavigationProperty(p.PropertyType))
    .ToList();

foreach (var navigationProperty in navigationProperties.Where(navigationProperty => navigationProperty.CanWrite)) {
    navigationProperty.SetValue(request.Model, null);
}
```

**Current Generator**: No stripping logic - can cause EF tracking issues.

---

### Gap #5: **Update Operation Semantics**

**CQRS**: Separate [`PatchHandler.cs`](src/Kernel/XFramework.Core/DataAccess/Commands/PatchHandler.cs:1) (partial update with upsert) and [`ReplaceHandler.cs`](src/Kernel/XFramework.Core/DataAccess/Commands/ReplaceHandler.cs:1) (full replacement)

**Current Generator**: Single `UpdateAsync` with unclear semantics (partial or full?)

---

### Gap #6: **Advanced Query Filtering**

**CQRS** ([`GetListHandler.cs:69-73`](src/Kernel/XFramework.Core/DataAccess/Query/GetListHandler.cs:69-73)):
```csharp
if (request.Filter != null && request.Filter.Any()) {
    var expression = request.Filter.ToExpression<TModel>();
    query = query.Where(expression!);
}
```

**Current Generator**: Basic pagination only - no dynamic filtering.

---

## 3. Enhancement Specifications

### Enhancement #1: Tenant Isolation (P0 - CRITICAL)

**Required Changes**:

1. **Add `ITenantService` to constructor** ([`EntityServiceGenerator.cs:262-270`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:262-270)):
```csharp
public TestProductService(
    DbContext dbContext,
    ICacheService cacheService,
    ITenantService tenantService,  // ← NEW
    ILogger<TestProductService> logger)
```

2. **Generate tenant helper method**:
```csharp
private async Task<Guid> GetCurrentTenantIdAsync(CancellationToken ct)
{
    var httpContext = _httpContextAccessor.HttpContext;
    if (httpContext?.Items.TryGetValue("TenantId", out var tenantIdObj) == true 
        && tenantIdObj is Guid tenantId)
        return tenantId;
    
    var tenant = await _tenantService.GetCurrentTenant(ct);
    return tenant.Id;
}
```

3. **Apply tenant filter to ALL queries**:
```csharp
// GetByIdAsync
var tenantId = await GetCurrentTenantIdAsync(ct);
var entity = await _dbContext.Set<TestProduct>()
    .AsNoTracking()
    .Where(e => e.TenantId == tenantId)  // ← NEW
    .Where(e => e.Id == id && !e.IsDeleted)
    .FirstOrDefaultAsync(ct);

// GetListAsync
query = query
    .AsNoTracking()
    .Where(e => e.TenantId == tenantId)  // ← NEW
    .Where(e => !e.IsDeleted);
```

4. **Include tenant in cache keys**:
```csharp
var cacheKey = $"testproducts:{tenantId}:{id}";  // ← Include tenant
```

**Files to Modify**:
- [`EntityServiceGenerator.cs:262-270`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:262-270) - Constructor
- [`EntityServiceGenerator.cs:315-360`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:315-360) - GetByIdMethod
- [`EntityServiceGenerator.cs:362-394`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:362-394) - GetListMethod
- [`EntityServiceGenerator.cs:277-313`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:277-313) - CreateMethod
- [`EntityServiceGenerator.cs:395-436`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:395-436) - UpdateMethod
- [`EntityServiceGenerator.cs:438-474`](src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs:438-474) - DeleteMethod

---

### Enhancement #2: Navigation Property Loading (P0 - CRITICAL)

**Required Changes**:

1. **Update request DTOs**:
```csharp
public class GetTestProductListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool IncludeNavigations { get; set; } = false;  // ← NEW
    public List<string>? Includes { get; set; }  // ← NEW
    public int NavigationDepth { get; set; } = 1;  // ← NEW
}
```

2. **Generate navigation methods** (copy logic from [`GetHandler.cs:93-132`](src/Kernel/XFramework.Core/DataAccess/Query/GetHandler.cs:93-132)):
```csharp
private IQueryable<TestProduct> IncludeNavigations(IQueryable<TestProduct> query, Type model, int maxDepth, int currentDepth = 0, string? modelName = "")
{
    if (currentDepth >= maxDepth || (typeof(TestProduct) == model && currentDepth > 0))
        return query;
    
    var navigationProperties = model.GetProperties()
        .Where(p => IsNavigationProperty(p.PropertyType))
        .ToList();
    
    foreach (var property in navigationProperties)
    {
        if (typeof(TestProduct) == property.PropertyType)
            continue;
        
        query = currentDepth == 0
            ? query.Include(property.Name)
            : query.Include($"{modelName}.{property.Name}");
        
        query = IncludeNavigationsForProperty(query, model, property.Name, maxDepth, currentDepth);
    }
    
    return query;
}

private bool IsNavigationProperty(Type type)
{
    return (type.IsClass && type != typeof(string) && type != typeof(byte[])) ||
           (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string) &&
            (type.GetGenericArguments().Any() ? type.GetGenericArguments()[0].IsClass : false));
}

private IQueryable<TestProduct> IncludeNavigationsForProperty(IQueryable<TestProduct> query, Type model, string propertyName, int maxDepth, int currentDepth)
{
    var propertyType = model.GetProperty(propertyName)!.PropertyType;
    var isCollection = typeof(IEnumerable).IsAssignableFrom(propertyType);
    var elementType = isCollection ? propertyType.GetGenericArguments()[0] : propertyType;
    return IncludeNavigations(query, elementType, maxDepth, currentDepth + 1, modelName: propertyName);
}
```

3. **Apply in GetListAsync and GetByIdAsync**:
```csharp
IQueryable<TestProduct> query = _dbContext.Set<TestProduct>();

if (request.IncludeNavigations)
{
    if (request.Includes is not null && request.Includes.Any())
    {
        query = request.Includes.Aggregate(query, (current, include) => current.Include(include));
    }
    else
    {
        query = IncludeNavigations(query, typeof(TestProduct), request.NavigationDepth);
    }
    
    query = query.AsSplitQuery();  // ← Prevent cartesian explosion
}
```

---

### Enhancement #3: Audit Field Management (P1 - HIGH)

**Required Changes**:

1. **Add `IHttpContextAccessor` to constructor**:
```csharp
private readonly IHttpContextAccessor _httpContextAccessor;

public TestProductService(
    DbContext dbContext,
    ICacheService cacheService,
    ITenantService tenantService,
    IHttpContextAccessor httpContextAccessor,  // ← NEW
    ILogger<TestProductService> logger)
```

2. **Generate user ID helper**:
```csharp
private string? GetCurrentUserId()
{
    return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
```

3. **Set audit fields in CreateAsync**:
```csharp
var userId = GetCurrentUserId();
var entity = new TestProduct
{
    // Property mappings...
    Id = Guid.NewGuid(),
    TenantId = tenantId,
    CreatedAt = DateTime.UtcNow,
    CreatedBy = userId,
    ConcurrencyStamp = Guid.NewGuid()
};
```

4. **Set audit fields in UpdateAsync**:
```csharp
entity.UpdatedAt = DateTime.UtcNow;
entity.UpdatedBy = userId;
entity.ConcurrencyStamp = Guid.NewGuid();
```

5. **Handle concurrency exceptions**:
```csharp
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogError(ex, "Concurrency conflict updating TestProduct {Id}", id);
    return Result<TestProduct>.Failure("Concurrency conflict occurred", 409);
}
```

**Attribute Option** (make optional if using `AuditInterceptor`):
```csharp
[GenerateEndpoints(
    ManageAuditFields = true  // ← NEW: Default true
)]
```

---

### Enhancement #4: Navigation Property Stripping (P1 - HIGH)

**Required Changes**:

Generate and call helper method before Add/SaveChanges:
```csharp
private void StripNavigationProperties(object model)
{
    var navigationProperties = model.GetType().GetProperties()
        .Where(p => IsNavigationProperty(p.PropertyType))
        .ToList();
    
    foreach (var navigationProperty in navigationProperties.Where(np => np.CanWrite))
    {
        navigationProperty.SetValue(model, null);
    }
}

// In CreateAsync before Add
StripNavigationProperties(entity);
_dbContext.Set<TestProduct>().Add(entity);

// In UpdateAsync before SaveChanges
StripNavigationProperties(entity);
await _dbContext.SaveChangesAsync(ct);
```

---

### Enhancement #5: Advanced Filtering (P2 - MEDIUM)

**Required Changes**:

1. **Add Filter to GetListRequest**:
```csharp
public class GetTestProductListRequest
{
    // ... existing properties
    public List<QueryFilter>? Filter { get; set; }  // ← NEW
}
```

2. **Apply in GetListAsync** (requires `QueryFilter.ToExpression<T>()` extension):
```csharp
if (request.Filter != null && request.Filter.Any())
{
    var expression = request.Filter.ToExpression<TestProduct>();
    query = query.Where(expression!);
}

query = query
    .AsNoTracking()
    .Where(e => e.TenantId == tenantId)
    .Where(e => !e.IsDeleted)
    .OrderBy(e => e.CreatedAt);  // ← Add default ordering
```

---

### Enhancement #6: Separate Update Operations (P2 - MEDIUM)

**Attribute Enhancement**:
```csharp
public enum UpdateMode
{
    Patch = 1,      // Partial update (like CQRS PatchHandler)
    Replace = 2,    // Full replacement (like CQRS ReplaceHandler)
    Both = 3        // Generate both methods
}

[GenerateEndpoints(
    UpdateMode = UpdateMode.Patch  // ← NEW: Default Patch
)]
```

**Generate PatchAsync** (with upsert):
```csharp
public virtual async Task<Result<TestProduct>> PatchAsync(Guid id, PatchTestProductRequest request, CancellationToken ct = default)
{
    var entity = await _dbContext.Set<TestProduct>()
        .Where(e => e.TenantId == tenantId)
        .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
    
    if (entity == null)
    {
        // Upsert behavior
        return await CreateAsync(MapToCreateRequest(request), ct);
    }
    
    entity = request.Adapt(entity);  // Partial update
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdatedBy = userId;
    entity.ConcurrencyStamp = Guid.NewGuid();
    
    // ... save and return
}
```

**Generate ReplaceAsync** (full replacement):
```csharp
public virtual async Task<Result<TestProduct>> ReplaceAsync(Guid id, ReplaceTestProductRequest request, CancellationToken ct = default)
{
    var entity = await _dbContext.Set<TestProduct>()
        .Where(e => e.TenantId == tenantId)
        .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
    
    if (entity == null)
        return Result<TestProduct>.NotFound($"TestProduct with ID {id} not found");
    
    entity = request;  // Full replacement
    entity.Id = id;  // Preserve ID
    entity.TenantId = tenantId;  // Preserve tenant
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdatedBy = userId;
    entity.ConcurrencyStamp = Guid.NewGuid();
    
    // ... save and return
}
```

---

## 4. Implementation Roadmap

### Phase 1: Critical Security & Functionality (Week 1)

**Sprint 1.1: Tenant Isolation** (2 days) - P0
- [ ] Add `ITenantService` to constructor generation
- [ ] Generate `GetCurrentTenantIdAsync()` helper
- [ ] Update all query methods with tenant filter
- [ ] Include tenant in cache keys
- [ ] **Test**: Multi-tenant data isolation
- [ ] **Test**: Cache key uniqueness per tenant

**Sprint 1.2: Navigation Loading** (3 days) - P0
- [ ] Generate `IncludeNavigations()` method
- [ ] Generate `IsNavigationProperty()` helper
- [ ] Generate `IncludeNavigationsForProperty()` helper
- [ ] Update GetByIdAsync with navigation support
- [ ] Update GetListAsync with navigation support
- [ ] Add `.AsSplitQuery()` when navigations included
- [ ] **Test**: Deep navigation loading (depth=3)
- [ ] **Test**: Explicit includes list
- [ ] **Test**: Circular reference prevention

**Sprint 1.3: Navigation Stripping** (1 day) - P1
- [ ] Generate `StripNavigationProperties()` method
- [ ] Call before Add in CreateAsync
- [ ] Call before SaveChanges in UpdateAsync
- [ ] **Test**: No navigation tracking issues

### Phase 2: Audit & Update Improvements (Week 2)

**Sprint 2.1: Audit Field Management** (2 days) - P1
- [ ] Add `IHttpContextAccessor` to constructor
- [ ] Generate `GetCurrentUserId()` helper
- [ ] Set audit fields in CreateAsync
- [ ] Set audit fields in UpdateAsync/DeleteAsync
- [ ] Add `ManageAuditFields` attribute option
- [ ] **Test**: Audit fields populated correctly
- [ ] **Test**: Audit interceptor compatibility

**Sprint 2.2: Separate Update Operations** (3 days) - P2
- [ ] Add `UpdateMode` enum to attribute
- [ ] Generate `PatchAsync` for partial updates
- [ ] Generate `ReplaceAsync` for full replacement
- [ ] Support upsert in PatchAsync
- [ ] **Test**: Patch vs Replace behavior
- [ ] **Test**: Upsert functionality

**Sprint 2.3: Concurrency Control** (1 day) - P1
- [ ] Set `ConcurrencyStamp` in updates
- [ ] Handle `DbUpdateConcurrencyException`
- [ ] Return 409 Conflict response
- [ ] **Test**: Concurrent update detection

### Phase 3: Advanced Features (Week 3)

**Sprint 3.1: Advanced Filtering** (2 days) - P2
- [ ] Add `Filter` property to GetListRequest DTOs
- [ ] Integrate `QueryFilter.ToExpression<T>()`
- [ ] Apply filters in GetListAsync
- [ ] Add default ordering (CreatedAt)
- [ ] **Test**: Complex filter expressions

**Sprint 3.2: Testing & Documentation** (2 days)
- [ ] Update TestProduct with all enhancements
- [ ] Create integration tests
- [ ] Performance benchmarks vs CQRS
- [ ] Update documentation
- [ ] Migration guide from CQRS to VSA

---

## 5. Testing Strategy

### Unit Tests (Roslyn-based)
```csharp
[Fact]
public async Task GeneratedService_GetById_EnforcesTenantIsolation()
{
    var tenant1Id = Guid.NewGuid();
    var tenant2Id = Guid.NewGuid();
    var productId = Guid.NewGuid();
    
    await SeedTestData(tenant1Id, productId, "Tenant1 Product");
    await SeedTestData(tenant2Id, productId, "Tenant2 Product");
    
    var result = await _service.GetByIdAsync(productId, tenant1Id, CancellationToken.None);
    
    result.IsSuccess.Should().BeTrue();
    result.Data.Name.Should().Be("Tenant1 Product");
    result.Data.TenantId.Should().Be(tenant1Id);
}

[Fact]
public async Task GeneratedService_GetListWithNavigations_LoadsRelatedEntities()
{
    var request = new GetTestProductListRequest
    {
        IncludeNavigations = true,
        NavigationDepth = 2
    };
    
    var result = await _service.GetListAsync(request, CancellationToken.None);
    
    result.Data.First().Category.Should().NotBeNull();
    result.Data.First().Category.Products.Should().NotBeNull();
}
```

### Integration Tests
- End-to-end CRUD cycle
- Multi-tenant isolation
- Cache consistency
- Navigation loading
- Concurrency control
- Performance vs CQRS

### Performance Targets
- **Response Time**: Generated services ≤ CQRS (ideally 20-30% faster due to no dispatcher overhead)
- **Memory Usage**: < 5% increase
- **Cache Hit Rate**: Maintain >80%

---

## 6. Success Metrics

### Code Quality
- **LOC Reduction**: 60-70% less boilerplate
- **Test Coverage**: Maintain >80%
- **Complexity**: Generated code ≤ hand-written CQRS

### Developer Experience
- **Setup Time**: New entity to working CRUD in < 10 minutes
- **Customization**: Partial class extensibility works
- **Documentation**: Clear examples for all features

### Business Value
- **Development Velocity**: 2x faster CRUD implementation
- **Maintenance Cost**: 50% reduction
- **Bug Rate**: Fewer bugs due to consistent code

---

## 7. Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Generated code has bugs | High | Medium | Comprehensive tests, phased rollout |
| Performance regression | High | Low | Benchmarks, monitoring, rollback plan |
| Tenant isolation failure | **Critical** | Low | Extensive testing, security audit |
| Breaking API changes | Medium | Low | Keep CQRS until full migration |

---

## 8. Dependencies (Already Exist ✅)

- ✅ `Result<T>` pattern
- ✅ `ICacheService` / `HybridCacheService`
- ✅ `ITenantService`
- ✅ `IHttpContextAccessor`
- ✅ Global query filters
- ✅ `AuditInterceptor`
- ✅ `QueryFilter.ToExpression<T>()`

**To Verify**:
- ⚠️ `IHttpContextAccessor` DI registration
- ⚠️ User ID claim configuration
- ⚠️ TypeSupport library (for Patch operations)

---

## 9. Recommendations

### Immediate Actions (Week 1)
1. **Enhancement #1 (Tenant Isolation)** - Critical security requirement
2. **Enhancement #2 (Navigation Loading)** - Essential for API completeness
3. **Enhancement #4 (Navigation Stripping)** - Prevents tracking bugs

### Strategic Decisions
- **Audit Fields**: Generate explicitly vs rely on interceptor
- **Validation**: Keep at endpoint level initially
- **Update Operations**: Implement Patch first, Replace optional

### Post-Implementation
- Document best practices
- Team training on new capabilities
- Create Visual Studio/Rider code snippets
- Add telemetry for performance tracking

---

## Conclusion

The `EntityServiceGenerator` has proven itself as a solid foundation. With these enhancements, it will fully replace CQRS infrastructure while providing superior developer experience and performance.

**Critical Path**: Enhancements #1 (Tenant) and #2 (Navigation) are mandatory for production use.

**Expected Outcome**: Developers add a single attribute and receive complete, secure, performant CRUD services that match or exceed CQRS capabilities.

**Next Steps**: Proceed to implementation following the 3-week roadmap.