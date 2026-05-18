---
description: Review code against XFramework standards
agent: build
---

# Review Code Against XFramework Best Practices

Review XFramework code against `docs/solutions/conventions/xframework-best-practices.md`.

Arguments: `$ARGUMENTS` should specify files, folders, or a module name to review. If empty, review staged and changed files.

Check for:
- VSA feature organization and no cross-feature imports.
- File-scoped namespaces, primary constructors, records for DTOs, required members, collection expressions, and pattern matching where appropriate.
- Minimal API endpoint quality: `TypedResults`, union return types, validation before service calls, route constraints, cancellation tokens, and OpenAPI metadata.
- Service quality: `Result<T>` or `Result`, no HTTP awareness, structured logging, OpenTelemetry activity tags, cancellation propagation, and cache invalidation.
- EF Core quality: `AsNoTracking`, projections for DTO reads, no N+1 queries, no `SaveChangesAsync` in loops, `AsSplitQuery` for multiple includes, and bulk update/delete APIs where appropriate.
- Caching, validation, security, pagination, and performance rules from the standards doc.

Output one section per reviewed file with `PASS`, `FAIL`, and `SKIP` bullets, then summarize issue counts by severity and list prioritized fixes.
