---
name: xframework-fix-caching
description: Fix XFramework caching usage, cache keys, invalidation, TTLs, and graceful degradation. Use when adding or reviewing caching in services or resolving stale-cache behavior.
---

# XFramework Caching Pattern Fixes

Review and fix XFramework caching usage in services or modules.

## When To Use

Use this skill when:
- The user asks to fix caching.
- Review finds stale cache risk, missing invalidation, weak key design, or unsafe TTLs.
- A bug involves stale or missing cached data.

## References

- `docs/solutions/conventions/xframework-best-practices.md`, section 9.
- `docs/solutions/best-practices/xframework-caching-strategy.md`.

## Check

- Cache keys use `{module}:{entity}:{identifier}`.
- Tenant-specific data includes tenant identity in the key.
- Writes invalidate both specific keys and list/query prefixes.
- TTLs are reasonable for data volatility.
- Cache failures do not crash the business operation.
- Mutable objects are not cached by shared reference when serialized copies are expected.

## Workflow

1. Read the service and related write/read paths together.
2. Identify all cache population and invalidation paths for the entity/query.
3. Normalize key naming and invalidation prefixes.
4. Preserve behavior except for fixing stale/unsafe cache behavior.
5. Run build or targeted tests when feasible.

Report cache key conventions, invalidation paths, and verification.
