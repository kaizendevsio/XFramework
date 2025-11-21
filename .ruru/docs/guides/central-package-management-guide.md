+++
id = "CPM-GUIDE-V1"
title = "Central Package Management (CPM) Guide"
description = "Guide for managing NuGet package versions using Central Package Management in XFramework"
context_type = "guide"
scope = "NuGet package version management across all projects"
target_audience = ["developers", "maintainers"]
granularity = "detailed"
status = "active"
last_updated = "2025-11-21"
tags = ["nuget", "cpm", "packages", "dependencies", "version-management"]
related_context = [
    ".ruru/docs/guides/dotnet10-csharp14-upgrade-strategy.md",
    "Directory.Packages.props",
    "AI-DEVELOPMENT-GUIDE.md"
]
+++

# Central Package Management (CPM) Guide

## Overview

XFramework uses .NET's **Central Package Management (CPM)** feature to manage all NuGet package versions from a single location. This provides consistency, simplifies updates, and reduces version conflicts across the 50+ projects in the solution.

**Implemented**: November 21, 2025 (Commit: `3cee2c4`)  
**Status**: ✅ Active (Build: 0 errors)

## What is CPM?

Central Package Management is a .NET SDK feature (introduced in .NET 7) that allows you to:
- Define all package versions in ONE central file (`Directory.Packages.props`)
- Reference packages in project files WITHOUT specifying versions
- Update package versions across the entire solution by changing a single file
- Ensure version consistency across all projects

## File Structure

```
XFramework/
├── Directory.Packages.props          # ← Central version management (140 packages)
├── src/
│   ├── Kernel/
│   │   └── XFramework.Core/
│   │       └── XFramework.Core.csproj   # ← No Version attributes
│   ├── Modules/
│   │   └── XFramework.*/
│   │       └── *.Api/*.csproj           # ← No Version attributes
│   └── ...
```

## How It Works

### 1. Directory.Packages.props (Central Definition)

Located at the solution root, this file defines ALL package versions:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Packages -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
    
    <!-- OpenTelemetry Suite (Standardized) -->
    <PackageVersion Include="OpenTelemetry" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    
    <!-- ... 140 packages total -->
  </ItemGroup>
</Project>
```

### 2. Project Files (.csproj) - No Versions

Projects reference packages WITHOUT version attributes:

```xml
<ItemGroup>
  <!-- ✅ CORRECT (CPM) -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="OpenTelemetry" />
  
  <!-- ❌ WRONG - Don't add Version attribute! -->
  <!-- <PackageReference Include="Serilog" Version="4.2.0" /> -->
  
  <!-- ✅ OK - Keep other attributes -->
  <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" 
                    PrivateAssets="all" />
</ItemGroup>
```

## Package Categories

### Core Framework Packages (Microsoft.*)
- **EntityFrameworkCore**: 9.0.0
- **AspNetCore**: 9.0.x series
- **Extensions**: 9.0.0 (Configuration, DependencyInjection, Logging, etc.)

### OpenTelemetry Suite (Standardized at 1.9.0)
- OpenTelemetry
- OpenTelemetry.Extensions.Hosting
- OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry.Instrumentation.EntityFrameworkCore (1.0.0-beta.12)
- OpenTelemetry.Instrumentation.Http
- OpenTelemetry.Instrumentation.StackExchangeRedis (1.0.0-rc9.14)
- OpenTelemetry.Exporter.Console
- OpenTelemetry.Exporter.OpenTelemetryProtocol

### Messaging & Serialization
- **MessagePack**: 3.1.0
- **MemoryPack**: 1.21.4
- **MediatR.Contracts**: 2.0.1 (kept for interfaces only)

### Validation & Mapping
- **FluentValidation**: 11.11.0 (consolidated from 9.3.0)
- **Mapster**: 7.4.0

### Logging & Monitoring
- **Serilog**: 4.2.0
- **Serilog.AspNetCore**: 9.0.0
- **Serilog.Sinks.***

### UI & Blazor
- **MudBlazor**: 8.5.1
- **Blazor components**: 9.0.3

## How to Update Packages

### Single Package Update

1. Open `Directory.Packages.props`
2. Find the package:
   ```xml
   <PackageVersion Include="Serilog" Version="4.2.0" />
   ```
3. Update the version:
   ```xml
   <PackageVersion Include="Serilog" Version="4.3.0" />
   ```
4. Build and test:
   ```bash
   dotnet build
   dotnet test
   ```

**That's it!** The change applies to ALL 50+ projects automatically.

### Multiple Package Updates

1. Edit `Directory.Packages.props`
2. Update multiple `<PackageVersion>` entries
3. Build once to verify all projects:
   ```bash
   dotnet build
   ```

### Adding a New Package

1. **Add to Directory.Packages.props**:
   ```xml
   <PackageVersion Include="NewtonSoft.Json" Version="13.0.3" />
   ```

2. **Reference in project file (NO version)**:
   ```xml
   <ItemGroup>
     <PackageReference Include="NewtonSoft.Json" />
   </ItemGroup>
   ```

3. Build and verify:
   ```bash
   dotnet build
   ```

## Version Conflict Resolutions (Historical)

During CPM implementation, these conflicts were resolved:

| Package | Old Versions | New Version | Rationale |
|---------|--------------|-------------|-----------|
| FluentValidation | 9.3.0, 11.11.0 | **11.11.0** | Latest stable |
| EntityFrameworkCore | 5.0.1, 7.0.0, 9.0.0 | **9.0.0** | .NET 10 standard |
| MediatR | 9.0.0, 12.4.1 | **REMOVED** | Migrated to VSA |
| AspNetCore.* | 5.0.x, 9.0.x | **9.0.x** | .NET 10 standard |

## Architecture Change: MediatR → VSA

As part of CPM implementation, MediatR was **completely removed** and replaced with Vertical Slice Architecture (VSA):

### Before (MediatR)
```csharp
// Old CQRS/MediatR pattern
public record GetUserQuery(Guid Id) : IRequest<Result<User>>;

public class GetUserHandler : IRequestHandler<GetUserQuery, Result<User>>
{
    public async Task<Result<User>> Handle(GetUserQuery request, CancellationToken ct)
    { ... }
}

// Usage
var result = await _mediator.Send(new GetUserQuery(id));
```

### After (VSA)
```csharp
// New VSA pattern
public record GetUserQuery(Guid Id) : IQuery<User>;

public class GetUserQueryHandler : IQueryHandler<GetUserQuery, User>
{
    public async Task<Result<User>> HandleAsync(GetUserQuery query, CancellationToken ct)
    { ... }
}

// Usage
var result = await _dispatcher.QueryAsync(new GetUserQuery(id));
```

**Benefits**:
- ✅ No heavy MediatR dependency
- ✅ Cleaner, simpler architecture
- ✅ Better performance (no pipeline overhead)
- ✅ Easier to understand and maintain

## Troubleshooting

### Problem: "Version attribute is not allowed on this element"

**Cause**: You added a Version attribute to a PackageReference in a .csproj file.

**Fix**:
```xml
<!-- ❌ WRONG -->
<PackageReference Include="Serilog" Version="4.2.0" />

<!-- ✅ CORRECT -->
<PackageReference Include="Serilog" />
```

The version MUST be in `Directory.Packages.props`, not in project files.

### Problem: "Package 'X' is not found"

**Cause**: Package is referenced in .csproj but not defined in Directory.Packages.props.

**Fix**: Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="X" Version="1.0.0" />
```

### Problem: Build fails with package version conflicts

**Cause**: Different projects need different versions of the same package.

**Solution**: CPM uses ONE version for all projects. You have two options:

1. **Preferred**: Update all projects to use the latest compatible version
2. **Workaround**: Override for specific projects (not recommended):
   ```xml
   <!-- In specific .csproj only -->
   <PackageReference Include="PackageName" VersionOverride="2.0.0" />
   ```

### Problem: "NU1507" warnings about multiple package sources

**Status**: ⚠️ Known issue (85 warnings)  
**Impact**: None (informational only)  
**Explanation**: Multiple NuGet sources are configured. Not a problem for development.

**To silence** (optional):
```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);NU1507</NoWarn>
</PropertyGroup>
```

## Best Practices

### ✅ DO

1. **Always update versions in Directory.Packages.props only**
2. **Keep package versions aligned across categories**
   - Example: All OpenTelemetry packages at 1.9.0
3. **Test after updating packages**:
   ```bash
   dotnet build && dotnet test
   ```
4. **Document major version changes** in commit messages
5. **Check for breaking changes** when updating major versions

### ❌ DON'T

1. **Never add Version attributes to .csproj files**
2. **Don't use different versions for the same package** across projects (defeats CPM purpose)
3. **Don't override versions** unless absolutely necessary
4. **Don't update packages** without testing the build

## Migration from Non-CPM

If you need to add a new project that doesn't use CPM yet:

1. **Remove all Version attributes** from PackageReference elements
2. **Verify all packages exist** in Directory.Packages.props
3. **Add missing packages** to Directory.Packages.props if needed
4. **Build and test**:
   ```bash
   dotnet build MyNewProject.csproj
   ```

## Benefits Summary

| Benefit | Before CPM | After CPM |
|---------|------------|-----------|
| **Update 1 package** | Edit 28+ files | Edit 1 file |
| **Version consistency** | Manual checks | Automatic |
| **Conflict resolution** | Project by project | Centralized |
| **Onboarding new devs** | Complex | Simple |
| **CI/CD reliability** | Version mismatches | Consistent |

## References

- **Official Docs**: [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- **XFramework Implementation**: Commit `3cee2c4`
- **.NET 10 Upgrade**: Commit `8e11737`
- **AI Development Guide**: `AI-DEVELOPMENT-GUIDE.md`

## Related Changes

- **.NET 10 Upgrade**: All projects upgraded to .NET 10 (`net10.0`)
- **C# 14**: Language version upgraded to C# 14
- **VSA Migration**: Complete removal of MediatR, implemented VSA pattern
- **Package Modernization**: All packages updated to latest stable versions

---

**Last Updated**: November 21, 2025  
**Status**: ✅ Production Ready  
**Build**: 0 errors, 89 warnings (all acceptable)