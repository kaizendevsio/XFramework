# Review Code Against XFramework Best Practices

You are reviewing code in the XFramework project against the established best practices.

## Context
Read the full best practices document at `docs/standards/xframework-best-practices.md`.

## Arguments
$ARGUMENTS should specify files, folders, or a module name to review. If empty, review all staged/changed files.

## Review Checklist

For each file reviewed, check against ALL applicable items:

### Architecture (Section 1-2)
- [ ] Code is organized by feature, not by layer
- [ ] No cross-feature imports (features don't reference each other)
- [ ] One type per file (exception: small request records in Endpoint.cs)
- [ ] File/folder naming follows conventions (PascalCase, correct suffixes)
- [ ] No `Controllers/` — everything uses Minimal API

### C# 14 / .NET 10 (Section 3)
- [ ] File-scoped namespaces
- [ ] Primary constructors for DI (no manual field assignment)
- [ ] `required` keyword on mandatory DTO properties
- [ ] Records for DTOs, classes for entities and services
- [ ] Collection expressions where applicable (`[1, 2, 3]` not `new List<int> { 1, 2, 3 }`)
- [ ] Pattern matching instead of nested if/else

### Endpoints (Section 4)
- [ ] Uses `TypedResults` (not anonymous objects or `Results.`)
- [ ] `Results<T1, T2, ...>` union return type declared
- [ ] `CancellationToken ct` as last parameter
- [ ] Validates before calling service
- [ ] Pattern matching to map Result<T> → HTTP
- [ ] Thin handler — no business logic
- [ ] Route constraints on parameters (`{id:guid}`)
- [ ] OpenAPI metadata (WithName, WithTags, WithDescription, Produces)

### Services (Section 5-6)
- [ ] Returns `Result<T>` or `Result` — never throws for expected failures
- [ ] No HTTP awareness (no HttpContext, StatusCodes, TypedResults)
- [ ] `CancellationToken ct` passed through entire chain
- [ ] Structured logging with `{PropertyName}` templates (no string interpolation)
- [ ] OpenTelemetry Activity with semantic tags
- [ ] Appropriate log levels (Debug/Info/Warning/Error)

### Data Access (Section 8)
- [ ] `AsNoTracking()` on read queries
- [ ] `.Select()` projections for read-only DTOs when possible
- [ ] No `SaveChangesAsync` inside loops
- [ ] `ExecuteUpdateAsync`/`ExecuteDeleteAsync` for bulk operations
- [ ] No manual soft-delete logic (rely on XDbContext)
- [ ] No manual tenant filtering (rely on global query filter)
- [ ] `AsSplitQuery()` when multiple `Include()` calls

### Caching (Section 9)
- [ ] Cache key format: `{module}:{entity}:{identifier}`
- [ ] Tenant ID in cache keys for tenant-specific data
- [ ] Invalidation on Create/Update/Delete
- [ ] Reasonable TTL (5-10 min default)
- [ ] Graceful degradation — no crash if Redis unavailable

### Validation (Section 7)
- [ ] One validator per request type
- [ ] Input shape only — no business rules
- [ ] Clear error messages
- [ ] Registered via assembly scanning

### Security (Section 13)
- [ ] No sensitive data in logs (passwords, tokens, PII)
- [ ] No client-provided tenant IDs trusted
- [ ] All user input validated

### Performance (Section 15)
- [ ] Pagination on list endpoints (never unbounded)
- [ ] CancellationToken propagated
- [ ] No N+1 queries

## Output Format

For each file, output:
```
### [filepath]
- PASS: [items that pass]
- FAIL: [items that fail with explanation and suggested fix]
- SKIP: [items not applicable to this file]
```

Then provide a summary with:
- Total files reviewed
- Total issues found (by severity: Critical / Warning / Info)
- Prioritized list of changes needed
