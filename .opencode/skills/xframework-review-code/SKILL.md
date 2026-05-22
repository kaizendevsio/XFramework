---
name: xframework-review-code
description: Review XFramework C#/.NET code against project conventions. Use when the user asks to review code, audit changed files, check a PR, or validate implementation quality in this repo.
---

# XFramework Code Review

Review XFramework code against `docs/solutions/conventions/xframework-best-practices.md` and relevant learnings in `docs/solutions/`.

## When To Use

Use this skill when:
- The user asks for a code review, module review, or PR readiness check.
- You have made non-trivial C#/.NET changes and need a project-standard pass.
- A change touches VSA endpoints, services, EF Core, caching, validation, logging, source generators, or tests.

## Workflow

1. Read `docs/solutions/conventions/xframework-best-practices.md`.
2. Use the most specific current subsystem doc in `docs/solutions/`; start with `docs/solutions/architecture-patterns/xframework-agent-architecture-surface-map.md` and `docs/solutions/conventions/xframework-feature-surface-map.md` when scope is unclear.
3. Search `docs/solutions/` for related module, component, or topic metadata.
4. Identify changed or requested files. If no scope is given, review staged and unstaged changes.
5. Review for correctness first, then maintainability, conventions, tests, performance, and security.
6. Report findings first, ordered by severity with file/line references.

## Review Checklist

- VSA feature organization and no cross-feature imports.
- File-scoped namespaces, primary constructors, records for DTOs, required members, collection expressions, and pattern matching where appropriate.
- Minimal API endpoint quality: generated `[Map*]` handlers return `Result<T>` or `Result`; `TypedResults` and union return types are for fully manual endpoints; validate before service calls, use route constraints, pass cancellation tokens, and provide OpenAPI metadata.
- Service quality: `Result<T>` or `Result`, no HTTP awareness, structured logging, OpenTelemetry activity tags, cancellation propagation, and cache invalidation.
- EF Core quality: avoid unintended tracking, use projections for DTO reads, no N+1 queries, no `SaveChangesAsync` in loops, `AsSplitQuery` for multiple includes, and bulk update/delete APIs where appropriate.
- Caching, validation, security, pagination, and performance rules from the standards doc.

## Output

Use this shape:

```markdown
Findings
- [severity] path:line - issue and why it matters

Open Questions
- question or assumption, if any

Summary
- brief change-quality summary
```

If no findings are found, say so explicitly and note residual risks or testing gaps.
