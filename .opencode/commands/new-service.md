---
description: Create or refactor a service
agent: build
---

# Create or Refactor a Service

Create or refactor an XFramework service following VSA service-layer standards.

Arguments: `$ARGUMENTS` should specify the module and service purpose, such as `Wallets WalletService` or `IdentityServer AuthService`.

Use `docs/solutions/conventions/xframework-best-practices.md` sections 5, 6, 8, 9, and 10. For data access and caching details, use `docs/solutions/conventions/ef-core-data-access-patterns.md` and `docs/solutions/best-practices/xframework-caching-strategy.md`. Use `src/Modules/XFramework.Inventario/Inventario.Api/Services/ProductService.cs` as a reference if it exists.

Rules:
- Prefer primary constructors for DI.
- Public async methods accept and propagate `CancellationToken ct`.
- Public methods return `Result<T>` or `Result` for expected outcomes.
- Keep HTTP concerns out of services.
- Use structured logging templates, not string interpolation.
- Add OpenTelemetry activity spans/tags when the surrounding code uses them.
- Use `AsNoTracking` and projections for read paths when possible.
- Use read-through caching and invalidate specific/list keys on writes where caching applies.
- Use cache keys in `{module}:{entity}:{identifier}` form and include tenant IDs for tenant-specific data.

After changes, report the design choices and run a build or targeted test if feasible.
