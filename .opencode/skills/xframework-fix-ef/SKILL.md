---
name: xframework-fix-ef
description: Fix Entity Framework Core query and persistence patterns in XFramework code. Use when reviewing EF queries, performance issues, N+1 behavior, tracking, projections, includes, or bulk updates.
---

# XFramework EF Core Pattern Fixes

Review and fix Entity Framework Core usage in XFramework code.

## When To Use

Use this skill when:
- The user asks to fix EF Core patterns.
- Code review finds query performance or tracking issues.
- A bug or slowdown involves database reads/writes.

## References

- `docs/solutions/conventions/xframework-best-practices.md`, sections 8 and 15.
- `docs/solutions/conventions/ef-core-data-access-patterns.md`.

## Find And Fix

- Unintended tracking on read queries; add explicit `AsNoTracking()` only when the default is overridden or unknown.
- Loading full entities when projection DTOs are enough.
- N+1 query patterns.
- `SaveChangesAsync` inside loops.
- Manual soft-delete or tenant filters that duplicate global query filters.
- Multiple includes without `AsSplitQuery()` where cartesian expansion is likely.
- Bulk changes that should use `ExecuteUpdateAsync` or `ExecuteDeleteAsync`.
- Writes that need tracking but use no-tracking results without `AsTracking()` or an explicit attach/update pattern.

## Workflow

1. Read target EF query code and relevant entity relationships.
2. Check whether global filters and query tracking defaults already apply.
3. Make the smallest safe query changes.
4. Avoid changing query semantics unless clearly intended.
5. Run build or focused tests when feasible.

For each fix, report file, issue, change made, and verification.
