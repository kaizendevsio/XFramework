+++
id = "TASK-PHASE5-1-ATTRIBUTES-20251120-145820"
title = "Phase 5.1: Define Source Generator Attributes"
status = "🟢 Done"
type = "🌟 Feature"
assigned_to = "util-senior-dev"
coordinator = "TASK-CMD-20251119-192100"
created_date = "2025-11-20T14:58:20Z"
updated_date = "2025-11-20T07:04:55Z"
related_docs = [
    "AI-DEVELOPMENT-GUIDE.md",
    "XFramework-Development-Roadmap.md",
    "src/Features/README.md"
]
tags = ["phase-5", "source-generators", "attributes", "code-generation", "vsa"]
+++

# Task: Phase 5.1 - Define Source Generator Attributes

## Description

Create the attribute definitions that will drive entity-centric source generation. These attributes will be applied to entity classes to automatically generate services and endpoints, further reducing boilerplate code in the VSA architecture.

## Context

**Current State:**
- Phase 4 completed: All 7 modules migrated to VSA manually
- Services created manually for each entity (~3,776 lines total)
- Endpoints created manually following VSA patterns

**Goal:**
- Define attributes that entities can use to opt-in to code generation
- Support flexible generation (services only, endpoints only, or both)
- Allow selective CRUD operation generation
- Provide foundation for Phase 5.2 (Service Generator) and 5.3 (Endpoint Generator)

**Reference:**
- Existing generator: `src/Modules/XFramework.Wallets/Wallets.Integration/Generators/HandlerGenerator.cs`
- VSA patterns established in Phase 1-4

## Acceptance Criteria

### 1. Create GenerateEndpointsAttribute
- [✅] Create `src/Kernel/XFramework.Core/Attributes/GenerateEndpointsAttribute.cs`
- [✅] Attribute targets `[AttributeUsage(AttributeTargets.Class)]`
- [✅] Properties:
  - `EndpointType Type { get; set; }` - What to generate
  - `EndpointActions Actions { get; set; }` - Which CRUD operations
  - `string RoutePrefix { get; set; }` - Base route (e.g., "api/products")
  - `bool RequireAuthorization { get; set; } = true` - Auth requirement
  - `string[] Roles { get; set; }` - Required roles (optional)
  - `int CacheDurationSeconds { get; set; } = 300` - Cache duration for GET operations
  - `string CacheKeyPrefix { get; set; }` - Cache key prefix
- [✅] Add XML documentation for all properties

### 2. Create EndpointType Enum
- [✅] Create `src/Kernel/XFramework.Core/Attributes/EndpointType.cs`
- [✅] Enum values:
  ```csharp
  public enum EndpointType
  {
      /// <summary>Generate service only (no endpoints)</summary>
      Service = 1,
      
      /// <summary>Generate minimal API endpoints only (no service)</summary>
      Rest = 2,
      
      /// <summary>Generate both service and endpoints</summary>
      Both = 3
  }
  ```
- [✅] Add XML documentation

### 3. Create EndpointActions Flags Enum
- [✅] Create `src/Kernel/XFramework.Core/Attributes/EndpointActions.cs`
- [✅] Flags enum:
  ```csharp
  [Flags]
  public enum EndpointActions
  {
      None = 0,
      Create = 1 << 0,    // POST
      Get = 1 << 1,       // GET /{id}
      GetList = 1 << 2,   // GET /
      Update = 1 << 3,    // PUT /{id}
      Delete = 1 << 4,    // DELETE /{id}
      
      // Convenience combinations
      All = Create | Get | GetList | Update | Delete,
      ReadOnly = Get | GetList,
      WriteOnly = Create | Update | Delete,
      Standard = Create | Get | GetList | Update  // All except Delete
  }
  ```
- [✅] Add XML documentation for each flag

### 4. Add Attribute Validation (Optional)
- [⏭️] Consider adding validation logic in attribute constructor (Deferred to Phase 5.2 - will be handled by generators)
- [⏭️] Validate `RoutePrefix` format (should start with "/" or "api/") (Deferred to Phase 5.2 - will be handled by generators)
- [⏭️] Validate `CacheDurationSeconds` > 0 (Deferred to Phase 5.2 - will be handled by generators)
- [⏭️] Add helpful error messages (Deferred to Phase 5.2 - will be handled by generators)

### 5. Documentation
- [✅] Create `docs/source-generators/attribute-usage-guide.md`
- [✅] Document attribute properties and their effects
- [ ] Provide usage examples:
  ```csharp
  // Example 1: Full CRUD with service and endpoints
  [GenerateEndpoints(
      Type = EndpointType.Both,
      Actions = EndpointActions.All,
      RoutePrefix = "api/products",
      RequireAuthorization = true,
      Roles = new[] { "Admin", "Manager" },
      CacheDurationSeconds = 600,
      CacheKeyPrefix = "products"
  )]
  public partial class Product : BaseEntity
  {
      // Entity properties...
  }
  
  // Example 2: Read-only endpoints
  [GenerateEndpoints(
      Type = EndpointType.Both,
      Actions = EndpointActions.ReadOnly,
      RoutePrefix = "api/lookup/categories",
      RequireAuthorization = false,
      CacheDurationSeconds = 3600
  )]
  public partial class Category : BaseEntity
  {
      // Entity properties...
  }
  
  // Example 3: Service only (manual endpoints)
  [GenerateEndpoints(
      Type = EndpointType.Service,
      Actions = EndpointActions.All
  )]
  public partial class ComplexEntity : BaseEntity
  {
      // Complex business logic requires custom endpoints
  }
  ```
- [✅] Document design decisions

### 6. Unit Tests (Optional but Recommended)
- [⏭️] Create test project for attributes if needed (Deferred to Phase 5.4 - Integration Testing)
- [⏭️] Test flag combinations (EndpointActions.Create | EndpointActions.Get) (Deferred to Phase 5.4 - Integration Testing)
- [⏭️] Test default values (Deferred to Phase 5.4 - Integration Testing)
- [⏭️] Test attribute application to classes (Deferred to Phase 5.4 - Integration Testing)

## Implementation Guidelines

### File Structure
```
src/Kernel/XFramework.Core/
└── Attributes/
    ├── GenerateEndpointsAttribute.cs
    ├── EndpointType.cs
    └── EndpointActions.cs

docs/
└── source-generators/
    └── attribute-usage-guide.md
```

### Attribute Design Principles
1. **Opt-in**: Entities must explicitly use the attribute
2. **Flexible**: Support partial generation (service OR endpoints)
3. **Granular**: Allow selecting specific CRUD operations
4. **Configurable**: Route, auth, caching all configurable
5. **Safe Defaults**: Secure by default (RequireAuthorization = true)

### Naming Conventions
- Attribute ends with "Attribute" suffix
- Enum values use PascalCase
- Flags use bit shift for clarity (1 << 0, 1 << 1, etc.)

### XML Documentation Standards
- [✅] Document what each property controls
- [✅] Provide examples in XML comments
- [✅] Explain default values
- [✅] Note any validation rules

## Related Components (Phase 5.2+)
These attributes will be consumed by:
- `EntityServiceGenerator.cs` (Phase 5.2) - Reads attribute to generate services
- `EntityEndpointGenerator.cs` (Phase 5.3) - Reads attribute to generate endpoints
- `MapGeneratedEndpoints()` extension (Phase 5.4) - Discovers attributed entities

## Success Metrics
- ✅ All 3 files created (Attribute + 2 Enums)
- ✅ Comprehensive XML documentation
- ✅ Usage guide with 3+ examples
- ✅ Attribute can be applied to entity classes
- ✅ Compiles without errors
- ✅ Design reviewed and approved

## Notes
- This is foundational work for the entire Phase 5
- Attributes must be in `XFramework.Core` (no generator dependencies)
- Keep it simple - complexity goes in the generators
- Consider future extensibility (e.g., custom serialization, versioning)
- After completion, Phase 5.2 (Service Generator) can begin

## Estimated Effort
- Implementation: 1-2 hours
- Documentation: 30 minutes
- Review: 15 minutes
- **Total: ~2.5 hours**