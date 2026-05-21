---
description: Fix EF Core data access patterns
agent: build
---

# Fix EF Core Data Access Patterns

Review and fix Entity Framework Core usage in the specified XFramework code.

Arguments: `$ARGUMENTS` should specify files, folders, or a module.

Use `docs/solutions/conventions/xframework-best-practices.md` sections 8 and 15 and `docs/solutions/conventions/ef-core-data-access-patterns.md`.

Find and fix:
- Unintended tracking on read queries; add explicit `AsNoTracking()` only when the default is overridden or unknown.
- Loading full entities when projection DTOs are enough.
- N+1 query patterns.
- `SaveChangesAsync` inside loops.
- Manual soft-delete or tenant filters that duplicate global query filters.
- Multiple includes without `AsSplitQuery()` where cartesian expansion is likely.
- Bulk changes that should use `ExecuteUpdateAsync` or `ExecuteDeleteAsync`.
- Writes that need tracking but use no-tracking results without `AsTracking()` or an explicit attach/update pattern.

For each fix, report file, issue, change made, and verification performed.
