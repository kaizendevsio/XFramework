---
description: Review code against XFramework standards
agent: build
---

# Review Code Against XFramework Best Practices

Review XFramework code against `docs/solutions/conventions/xframework-best-practices.md` and the most specific current subsystem doc in `docs/solutions/`.

Arguments: `$ARGUMENTS` should specify files, folders, or a module name to review. If empty, review staged and changed files.

Check for:
- VSA feature organization and no cross-feature imports.
- File-scoped namespaces, primary constructors, records for DTOs, required members, collection expressions, and pattern matching where appropriate.
- Minimal API endpoint quality: generated `[Map*]` handlers return `Result<T>` or `Result`; `TypedResults` and union return types are for fully manual endpoints; validate before service calls, use route constraints, pass cancellation tokens, and provide OpenAPI metadata.
- Service quality: `Result<T>` or `Result`, no HTTP awareness, structured logging, OpenTelemetry activity tags, cancellation propagation, and cache invalidation.
- EF Core quality: avoid unintended tracking, use projections for DTO reads, no N+1 queries, no `SaveChangesAsync` in loops, `AsSplitQuery` for multiple includes, and bulk update/delete APIs where appropriate.
- Caching, validation, security, pagination, and performance rules from the standards doc.

Use `docs/solutions/conventions/ef-core-data-access-patterns.md`, `docs/solutions/best-practices/xframework-caching-strategy.md`, `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md`, and `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md` when those surfaces are in scope.

Output one section per reviewed file with `PASS`, `FAIL`, and `SKIP` bullets, then summarize issue counts by severity and list prioritized fixes.
