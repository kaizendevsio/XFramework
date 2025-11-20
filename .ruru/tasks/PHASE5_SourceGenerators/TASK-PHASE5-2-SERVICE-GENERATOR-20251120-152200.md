+++
id = "TASK-PHASE5-2-SERVICE-GENERATOR-20251120-152200"
title = "Phase 5.2: Entity Service Generator Implementation"
status = "🟡 To Do"
type = "🌟 Feature"
assigned_to = "util-senior-dev"
coordinator = "TASK-CMD-20251119-192100"
created_date = "2025-11-20T15:22:00Z"
updated_date = "2025-11-20T15:22:00Z"
related_docs = [
    "AI-DEVELOPMENT-GUIDE.md",
    "XFramework-Development-Roadmap.md",
    "docs/source-generators/attribute-usage-guide.md",
    "src/Kernel/XFramework.Core/Attributes/GenerateEndpointsAttribute.cs"
]
tags = ["phase-5", "source-generators", "roslyn", "code-generation", "services", "vsa"]
+++

# Task: Phase 5.2 - Entity Service Generator Implementation

## Description

Implement a Roslyn source generator that reads `GenerateEndpointsAttribute` from entity classes and automatically generates partial service classes with CRUD methods. This significantly reduces boilerplate by auto-generating the ~200-400 lines of service code we've been writing manually.

## Context

**Prerequisites (Completed):**
- ✅ Phase 5.1: Attributes defined (GenerateEndpointsAttribute, EndpointType, EndpointActions)
- ✅ Result<T> pattern implemented
- ✅ VSA patterns established across 7 modules

**Current State:**
- Services manually written (~3,776 lines across 7 modules)
- Each service follows consistent patterns (CRUD, Result<T>, caching, logging)

**Goal:**
- Automate service generation from entity attributes
- Generate partial classes allowing developer overrides
- Support selective CRUD operation generation
- Include proper error handling, logging, caching
- Generate XML documentation

**Reference Implementations:**
- Existing services: WalletService (1,168 lines), IdentityServer AuthService (963 lines)
- Existing generator: `Wallets.Integration/Generators/HandlerGenerator.cs`

## Acceptance Criteria

### 1. Create Source Generator Project
- [ ] Create new project: `src/SourceGenerators/XFramework.SourceGenerators/XFramework.SourceGenerators.csproj`
- [ ] Target: `<TargetFramework>netstandard2.0</TargetFramework>` (required for source generators)
- [ ] Add NuGet packages:
  - `Microsoft.CodeAnalysis.CSharp` (latest)
  - `Microsoft.CodeAnalysis.Analyzers` (latest)
- [ ] Add project reference to `XFramework.Core` (for accessing attributes)
- [ ] Configure as source generator:
  ```xml
  <PropertyGroup>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>
  ```

### 2. Implement EntityServiceGenerator
- [ ] Create `src/SourceGenerators/XFramework.SourceGenerators/EntityServiceGenerator.cs`
- [ ] Implement `IIncrementalGenerator` interface
- [ ] Initialize method:
  ```csharp
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
      // Register syntax provider to find attributed classes
      // Transform to extract entity info
      // Register source output
  }
  ```
- [ ] Entity discovery logic:
  - Find all classes with `[GenerateEndpoints]` attribute
  - Where `Type` is `Service` or `Both`
  - Extract entity name, namespace, properties
  - Extract `Actions` flags to determine which methods to generate

### 3. Generate Service Interface
- [ ] Generate `I{Entity}Service` interface
- [ ] Include methods based on `EndpointActions` flags:
  ```csharp
  public interface IProductService
  {
      // If Actions.Create is set
      Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
      
      // If Actions.Get is set
      Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default);
      
      // If Actions.GetList is set
      Task<Result<List<Product>>> GetListAsync(GetProductsRequest request, CancellationToken ct = default);
      
      // If Actions.Update is set
      Task<Result<Product>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
      
      // If Actions.Delete is set
      Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
  }
  ```
- [ ] Add XML documentation to all interface methods
- [ ] Generate in same namespace as entity

### 4. Generate Service Implementation
- [ ] Generate **partial** `{Entity}Service` class
- [ ] Mark all methods as **virtual** (allows override in user-defined partial class)
- [ ] Inject dependencies:
  ```csharp
  public partial class ProductService : IProductService
  {
      private readonly DbContext _dbContext;
      private readonly ICacheService _cacheService;
      private readonly ILogger<ProductService> _logger;
      
      public ProductService(
          DbContext dbContext,
          ICacheService cacheService,
          ILogger<ProductService> logger)
      {
          _dbContext = dbContext;
          _cacheService = cacheService;
          _logger = logger;
      }
  }
  ```
- [ ] Generate each CRUD method based on Actions flags

### 5. Generate CRUD Methods
Each generated method should follow this pattern:

**CreateAsync:**
```csharp
public virtual async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
{
    try
    {
        var entity = new Product
        {
            // Map properties from request
            // Use property mapping logic
        };
        
        _dbContext.Set<Product>().Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        
        // Invalidate cache
        await _cacheService.RemoveByPrefixAsync("products:", ct);
        
        _logger.LogInformation("Created {EntityType} with ID {Id}", nameof(Product), entity.Id);
        
        return Result<Product>.Success(entity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating {EntityType}", nameof(Product));
        return Result<Product>.Failure($"Failed to create {nameof(Product)}", 500);
    }
}
```

**GetByIdAsync:**
```csharp
public virtual async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    try
    {
        var cacheKey = $"products:{id}";
        
        // Try cache first
        var cached = await _cacheService.GetAsync<Product>(cacheKey, ct);
        if (cached.IsSuccess && cached.Data != null)
            return Result<Product>.Success(cached.Data);
        
        // Query database with AsNoTracking
        var entity = await _dbContext.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        
        if (entity == null)
            return Result<Product>.NotFound($"{nameof(Product)} with ID {id} not found");
        
        // Cache result
        await _cacheService.SetAsync(cacheKey, entity, 
            absoluteExpiration: TimeSpan.FromSeconds(CacheDurationSeconds), ct);
        
        return Result<Product>.Success(entity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving {EntityType} {Id}", nameof(Product), id);
        return Result<Product>.Failure($"Failed to retrieve {nameof(Product)}", 500);
    }
}
```

**GetListAsync:**
```csharp
public virtual async Task<Result<List<Product>>> GetListAsync(GetProductsRequest request, CancellationToken ct = default)
{
    try
    {
        var query = _dbContext.Set<Product>()
            .AsNoTracking()
            .Where(e => !e.IsDeleted);
        
        // Apply filters from request if any
        // Apply pagination
        var entities = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);
        
        return Result<List<Product>>.Success(entities);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving {EntityType} list", nameof(Product));
        return Result<List<Product>>.Failure($"Failed to retrieve {nameof(Product)} list", 500);
    }
}
```

**UpdateAsync:**
```csharp
public virtual async Task<Result<Product>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
{
    try
    {
        var entity = await _dbContext.Set<Product>()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        
        if (entity == null)
            return Result<Product>.NotFound($"{nameof(Product)} with ID {id} not found");
        
        // Update properties from request
        // Use property mapping logic
        
        await _dbContext.SaveChangesAsync(ct);
        
        // Invalidate cache
        await _cacheService.RemoveAsync($"products:{id}", ct);
        await _cacheService.RemoveByPrefixAsync("products:", ct);
        
        _logger.LogInformation("Updated {EntityType} with ID {Id}", nameof(Product), id);
        
        return Result<Product>.Success(entity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating {EntityType} {Id}", nameof(Product), id);
        return Result<Product>.Failure($"Failed to update {nameof(Product)}", 500);
    }
}
```

**DeleteAsync (Soft Delete):**
```csharp
public virtual async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
{
    try
    {
        var entity = await _dbContext.Set<Product>()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        
        if (entity == null)
            return Result<bool>.NotFound($"{nameof(Product)} with ID {id} not found");
        
        entity.IsDeleted = true;
        await _dbContext.SaveChangesAsync(ct);
        
        // Invalidate cache
        await _cacheService.RemoveAsync($"products:{id}", ct);
        await _cacheService.RemoveByPrefixAsync("products:", ct);
        
        _logger.LogInformation("Deleted {EntityType} with ID {Id}", nameof(Product), id);
        
        return Result<bool>.Success(true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting {EntityType} {Id}", nameof(Product), id);
        return Result<bool>.Failure($"Failed to delete {nameof(Product)}", 500);
    }
}
```

### 6. Generate XML Documentation
- [ ] Add XML docs to all generated methods
- [ ] Include parameter descriptions
- [ ] Include return value descriptions
- [ ] Include exception documentation

### 7. Handle Compilation Context
- [ ] Add proper `#nullable enable` directives
- [ ] Add `#pragma warning disable` for generated code warnings
- [ ] Add generation timestamp comment
- [ ] Add "Auto-generated code" header

### 8. Testing
- [ ] Create test entity with `[GenerateEndpoints(Type = EndpointType.Service, Actions = EndpointActions.All)]`
- [ ] Verify service interface is generated
- [ ] Verify service implementation is generated
- [ ] Verify all CRUD methods present
- [ ] Test partial class override (create manual partial with custom method)
- [ ] Verify build succeeds
- [ ] Test with different Actions combinations (ReadOnly, WriteOnly, etc.)

### 9. Documentation
- [ ] Update `docs/source-generators/attribute-usage-guide.md` with service generator examples
- [ ] Document partial class override pattern
- [ ] Document how to customize generated methods
- [ ] Add troubleshooting section

## Technical Implementation Notes

### Source Generator Setup
```xml
<!-- In consumer project (e.g., Inventario.Core) -->
<ItemGroup>
  <ProjectReference Include="..\..\SourceGenerators\XFramework.SourceGenerators\XFramework.SourceGenerators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### Entity Discovery Pattern
```csharp
var classDeclarations = context.SyntaxProvider
    .CreateSyntaxProvider(
        predicate: (s, _) => s is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
        transform: (ctx, _) => GetSemanticTargetForGeneration(ctx))
    .Where(m => m is not null);
```

### Code Generation Template
Use **string interpolation** for code generation:
```csharp
var source = $$"""
namespace {{entityNamespace}}
{
    {{GenerateInterface(entity)}}
    
    {{GenerateImplementation(entity)}}
}
""";

context.AddSource($"{entityName}Service.g.cs", SourceText.From(source, Encoding.UTF8));
```

### Property Mapping Strategy
- For Create: Map all writable properties from request to entity
- For Update: Map all writable properties except Id, CreatedAt, CreatedBy
- Use reflection or Roslyn symbol analysis to determine properties

### Cache Key Convention
- Single entity: `{entityNameLower}:{id}` (e.g., `products:123`)
- List/prefix: `{entityNameLower}:` (e.g., `products:`)
- Use `CacheKeyPrefix` from attribute if provided

### Error Handling
- All methods use try-catch
- Log errors with structured logging
- Return appropriate Result<T> failures
- Never throw exceptions directly

## Complexity Considerations

This is a **complex task** involving:
- Roslyn compiler APIs (syntax trees, semantic models)
- Code generation with proper formatting
- String manipulation for code templates
- Conditional logic based on Actions flags
- Testing source generator behavior

**Estimated Effort:** 6-8 hours

## Success Metrics
- ✅ Source generator project compiles
- ✅ Generator discovers attributed entities
- ✅ Interface and implementation generated
- ✅ All 5 CRUD methods generated correctly
- ✅ Generated code compiles without errors
- ✅ Partial class override works
- ✅ Different Actions combinations work
- ✅ XML documentation present

## Notes
- Start simple: Generate for EndpointActions.All first
- Add complexity incrementally (selective actions, caching, etc.)
- Use existing services as reference for patterns
- Test frequently during development
- Consider performance: Source generators run on every keystroke
- Use incremental generation for performance

## Phase Context
After this, Phase 5.3 (Endpoint Generator) will consume the same attributes to generate minimal API endpoints that call these generated services.