---
description: Fix EF Core data access patterns
agent: build
---

# Fix EF Core Data Access Patterns

Review and fix Entity Framework Core usage in the specified XFramework code.

Arguments: `$ARGUMENTS` should specify files, folders, or a module.

Use `docs/solutions/conventions/xframework-best-practices.md` sections 8 and 15.

Find and fix:
- Missing explicit `AsNoTracking()` on read queries.
- Loading full entities when projection DTOs are enough.
- N+1 query patterns.
- `SaveChangesAsync` inside loops.
- Manual soft-delete or tenant filters that duplicate global query filters.
- Multiple includes without `AsSplitQuery()` where cartesian expansion is likely.
- Bulk changes that should use `ExecuteUpdateAsync` or `ExecuteDeleteAsync`.

For each fix, report file, issue, change made, and verification performed.
