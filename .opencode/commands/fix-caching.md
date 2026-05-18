---
description: Fix caching usage patterns
agent: build
---

# Fix Caching Patterns

Review and fix XFramework caching usage in the specified service or module.

Arguments: `$ARGUMENTS` should specify the service, folder, or module to review.

Use `docs/solutions/conventions/xframework-best-practices.md` section 9 and `docs/solutions/best-practices/xframework-caching-strategy.md`.

Check:
- Cache keys use `{module}:{entity}:{identifier}`.
- Tenant-specific data includes tenant identity in the key.
- Writes invalidate both specific keys and list/query prefixes.
- TTLs are reasonable for data volatility.
- Cache failures do not crash the business operation.
- Mutable objects are not cached by shared reference when serialized copies are expected.

After changes, report cache key conventions, invalidation paths, and verification performed.
