# .NET 10 & C# 14 Upgrade Strategy for XFramework

**Status**: In Progress  
**Target**: .NET 10 with C# 14 language features  
**Current**: .NET 9 with C# 13  
**Date**: 2025-11-21

## Overview

This document outlines the strategy for upgrading XFramework from .NET 9 (C# 13) to .NET 10 (C# 14), including the systematic application of new language features to improve code quality, performance, and maintainability.

## Phase 1: Version Updates (Foundation)

### 1.1 Update SDK and Target Framework
- [ ] **global.json**: Update SDK version from `9.0.0` to `10.0.0`
- [ ] **All .csproj files**: Update `<TargetFramework>` from `net9.0` to `net10.0`
- [ ] **All .csproj files**: Update `<LangVersion>` from `13` to `14`

### 1.2 Package Updates
- [ ] Review and update NuGet packages for .NET 10 compatibility
- [ ] Update OpenTelemetry packages to latest versions
- [ ] Update EF Core packages to 10.x versions
- [ ] Update ASP.NET Core packages to 10.x versions

**Responsibility**: Delegate to `util-senior-dev` or `dev-core-web`

---

## Phase 2: C# 13 Features (Already Available, Verify Usage)

### 2.1 `params` Collections Enhancement ✅
**Impact**: High (Performance)  
**Applicable**: Services, Extension Methods

```csharp
// OLD: params array
public void LogMessages(params string[] messages) { }

// NEW: params span (better performance, no allocation)
public void LogMessages(params ReadOnlySpan<string> messages) { }
```

**Target Files**:
- `XFramework.Core/Extensions/*.cs`
- `XFramework.Core/Services/**/*.cs`
- Logging helpers

### 2.2 New Lock Type ✅
**Impact**: Medium (Performance & Safety)  
**Applicable**: Thread-safe services, caching

```csharp
// OLD
private readonly object _lock = new();
lock (_lock) { /* ... */ }

// NEW (better performance)
private readonly Lock _lock = new();
lock (_lock) { /* ... */ }
```

**Target Files**:
- `XFramework.Core/Services/Caching/HybridCacheService.cs`
- `XFramework.Integration/Services/CacheManager.cs`
- Any service with thread synchronization

### 2.3 Implicit Index Access ✅
**Impact**: Low (Readability)  
**Applicable**: Array/collection initializers

```csharp
// NEW: Use ^1, ^2 for "from end" indexing
var buffer = new int[10]
{
    [^1] = 0,  // Last element
    [^2] = 1,  // Second to last
};
```

### 2.4 Partial Properties ✅
**Impact**: Medium (Generated Code)  
**Applicable**: Source generators, partial classes

```csharp
// Enables properties in partial classes (good for source generators)
public partial class UserService
{
    public partial string CacheKey { get; set; }
}
```

**Target Files**:
- `XFramework.SourceGenerators/**/*.cs`
- Generated service partial classes

---

## Phase 3: C# 14 Features (New Capabilities)

### 3.1 `field` Keyword 🌟
**Impact**: HIGH (Code Quality)  
**Applicable**: All properties with validation/logic

```csharp
// OLD: Manual backing field
private string _name;
public string Name
{
    get => _name;
    set => _name = value ?? throw new ArgumentNullException(nameof(value));
}

// NEW: Automatic backing field via 'field' keyword
public string Name
{
    get;
    set => field = value ?? throw new ArgumentNullException(nameof(value));
}
```

**Target Files**:
- `XFramework.Domain/**/*.cs` - Domain entities
- `XFramework.Core/Services/**/*.cs` - Service classes
- All DTO/Request/Response classes

**Estimated Improvement**: ~30-40% less boilerplate code

### 3.2 Null-Conditional Assignment 🌟
**Impact**: HIGH (Code Quality)  
**Applicable**: All services, middleware, handlers

```csharp
// OLD: Explicit null check
if (customer is not null)
{
    customer.Order = GetCurrentOrder();
}

// NEW: Null-conditional assignment
customer?.Order = GetCurrentOrder();
```

**Target Files**:
- All service methods
- Middleware classes
- Request handlers
- Repository methods

**Estimated Improvement**: 20-30% less null-check boilerplate

### 3.3 Extension Members 🌟
**Impact**: HIGH (API Design)  
**Applicable**: Extension methods, new API patterns

```csharp
// NEW: Extension properties and static extension methods
extension<T>(IEnumerable<T> source)
{
    // Extension property (instance)
    public bool IsEmpty => !source.Any();
    
    // Extension method (instance)
    public IEnumerable<T> WhereActive() 
        => source.Where(x => x is IHasStatus { Status: "Active" });
}

// Static extensions
extension<T>(IEnumerable<T>)
{
    // Static extension property
    public static IEnumerable<T> Empty => Enumerable.Empty<T>();
}
```

**Target Files**:
- `XFramework.Core/Extensions/**/*.cs`
- Create new extension blocks for common patterns
- Result<T> extensions
- Query extensions for EF Core

**Estimated Improvement**: Better discoverability, cleaner API

### 3.4 Implicit Span Conversions 🌟
**Impact**: MEDIUM (Performance)  
**Applicable**: Performance-critical code, parsing, buffers

```csharp
// Better span conversions, more natural usage
ReadOnlySpan<char> span = "Hello World";  // Direct string → span
Span<int> numbers = stackalloc int[] { 1, 2, 3 };

// Works better with generics now
public void Process<T>(ReadOnlySpan<T> items) where T : allows ref struct
{
    // Can now use Span<T> in more generic contexts
}
```

**Target Files**:
- `XFramework.Core/Services/**/*.cs` - String processing
- Parsing/validation logic
- Performance-critical paths

### 3.5 `nameof` with Unbound Generics
**Impact**: LOW (Logging/Diagnostics)  
**Applicable**: Logging, exception messages

```csharp
// OLD: Had to use closed generic
throw new InvalidOperationException($"Cannot create {nameof(List<int>)}");

// NEW: Can use unbound generic
throw new InvalidOperationException($"Cannot create {nameof(List<>)}");
```

**Target Files**:
- Logging statements
- Exception messages
- Diagnostic code

### 3.6 Lambda Parameter Modifiers
**Impact**: MEDIUM (Code Quality)  
**Applicable**: LINQ queries, delegates

```csharp
// NEW: Add modifiers without explicit types
Func<string, bool> parser = (in text) => TryParse(text);
Action<int> processor = (ref value) => value *= 2;
```

**Target Files**:
- LINQ queries with ref parameters
- Delegate definitions
- Callback handlers

### 3.7 Partial Events and Constructors
**Impact**: MEDIUM (Generated Code)  
**Applicable**: Source generators

```csharp
// Enables partial constructors and events
public partial class Service
{
    public partial Service();  // Declaring
    public partial event EventHandler Changed;
}

public partial class Service
{
    public partial Service()   // Implementing
    {
        // Constructor body
    }
    
    public partial event EventHandler Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }
}
```

**Target Files**:
- Source generator outputs
- Partial class definitions

---

## Phase 4: Implementation Order

### Priority 1: Foundation (Week 1)
1. ✅ Update version numbers (global.json, all .csproj)
2. ✅ Update package dependencies
3. ✅ Build and verify no breaking changes
4. ✅ Run existing tests

**Delegate to**: `util-senior-dev`

### Priority 2: High-Impact Language Features (Week 1-2)
1. Apply `field` keyword across domain entities and DTOs
2. Apply null-conditional assignment across services
3. Refactor common extension methods to extension members
4. Update lock statements to use new Lock type

**Delegate to**: 
- `dev-core-web` for Core/Integration
- `util-refactor` for systematic refactoring
- `util-senior-dev` for complex scenarios

### Priority 3: Performance Optimizations (Week 2)
1. Convert params arrays to params spans
2. Apply implicit span conversions where beneficial
3. Review and optimize string processing with spans

**Delegate to**: `util-performance`

### Priority 4: Code Quality (Week 3)
1. Add lambda parameter modifiers where applicable
2. Update nameof usage with unbound generics
3. Review partial members for source generators

**Delegate to**: `util-refactor`

---

## Success Metrics

### Code Quality
- **Boilerplate Reduction**: Target 25-35% reduction in property/validation code
- **Null-Check Reduction**: Target 20-30% reduction in explicit null checks
- **API Clarity**: Extension members improve discoverability

### Performance
- **Allocation Reduction**: params spans reduce allocations in hot paths
- **Lock Performance**: New Lock type improves contention scenarios
- **Span Usage**: Better span conversions improve string operations

### Build & Tests
- ✅ Zero build errors after upgrade
- ✅ All existing tests pass
- ✅ No new warnings introduced
- ✅ Performance benchmarks maintained or improved

---

## Risk Mitigation

### Breaking Changes
- Review breaking changes document for C# 14
- Test all public APIs after upgrade
- Verify source generator outputs

### Compatibility
- Ensure all NuGet packages support .NET 10
- Test in local development environment first
- Verify Docker images use .NET 10 SDK

### Rollback Plan
- Changes committed incrementally by module
- Each phase is independently testable
- Can rollback to .NET 9 if critical issues found

---

## Module-by-Module Approach

### 1. XFramework.Core (Foundation)
**Features**: field, null-conditional, extension members, new lock
**Estimated Effort**: 4-6 hours
**Impact**: High (affects all modules)

### 2. XFramework.Domain
**Features**: field, partial properties
**Estimated Effort**: 2-3 hours
**Impact**: High (domain entities)

### 3. XFramework.Integration
**Features**: field, null-conditional, params spans
**Estimated Effort**: 3-4 hours
**Impact**: Medium

### 4. Feature Modules (8 modules)
**Features**: Apply patterns from Core/Domain
**Estimated Effort**: 1-2 hours per module
**Impact**: Medium

### 5. SourceGenerators
**Features**: Partial events/constructors, updated templates
**Estimated Effort**: 2-3 hours
**Impact**: Low (but important for code generation)

---

## Documentation Updates Required

- [ ] Update AI-DEVELOPMENT-GUIDE.md with C# 14 patterns
- [ ] Create C# 14 best practices guide
- [ ] Update code style guidelines
- [ ] Add examples for new features
- [ ] Update VSA migration guide with new patterns

---

## Next Actions

1. **Coordinator Reviews**: Present strategy to user for approval
2. **Phase 1 Execution**: Delegate version updates
3. **Phase 2 Execution**: Begin systematic code improvements
4. **Testing & Validation**: Build, test, verify at each step
5. **Documentation**: Update all relevant documentation

---

**Prepared by**: Roo Commander  
**Review Required**: User approval before proceeding  
**Estimated Total Effort**: 2-3 weeks for complete upgrade