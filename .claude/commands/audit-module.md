# Audit an Entire Module Against Best Practices

You are performing a comprehensive audit of an XFramework module against the best practices.

## Context
Read `docs/standards/xframework-best-practices.md` for the full standards reference.

## Arguments
$ARGUMENTS should specify the module name (e.g., "Inventario", "Wallets", "IdentityServer").

## Steps

1. **Find the module** at `src/Modules/XFramework.[ModuleName]/`
2. **Read ALL code files** in the module's `.Api` project:
   - Program.cs
   - All files in Features/
   - All files in Services/
   - All files in Installers/
   - The .csproj file
   - GlobalUsings.cs
   - Any Entities/ files
3. **Review each file** against the best practices checklist (use the review-code standards)
4. **Produce an audit report** with findings and recommendations

## Audit Categories

### A. Structure & Architecture
- Does the module follow VSA folder structure?
- Are features properly isolated?
- Is there a clean aggregator pattern?
- Is Program.cs minimal and using XApplication conventions?

### B. C# 14 / .NET 10 Modernization
- Primary constructors used?
- File-scoped namespaces everywhere?
- Records for DTOs?
- `required` keyword on mandatory properties?
- Collection expressions?
- Any deprecated patterns that need updating?

### C. Endpoint Quality
- All endpoints using TypedResults?
- Proper union return types?
- CancellationToken on every handler?
- Validation before service call?
- Pattern matching for Result → HTTP mapping?
- OpenAPI metadata complete?

### D. Service Quality
- Result<T> returns on all public methods?
- No HTTP awareness leaking in?
- Structured logging with correct levels?
- OpenTelemetry instrumentation?
- Proper caching with invalidation?
- CancellationToken propagated?

### E. Data Access
- AsNoTracking on reads?
- Projections where possible?
- No N+1 queries?
- Proper use of Include/AsSplitQuery?
- Relying on global filters (not manual soft-delete/tenant checks)?

### F. Package Alignment
- .csproj targeting net10.0 with LangVersion 14?
- NuGet packages at correct versions?
- No unnecessary package references?

## Output Format

```markdown
# Module Audit: [ModuleName]
**Date:** [today]
**Files Reviewed:** [count]
**Overall Score:** [A/B/C/D/F]

## Summary
[2-3 sentence executive summary]

## Critical Issues (must fix)
1. [issue + file + line + fix]

## Warnings (should fix)
1. [issue + file + line + fix]

## Modernization Opportunities
1. [improvement + file + suggested change]

## What's Working Well
1. [positive finding]

## Recommended Action Plan
1. [ordered list of changes to make]
```
