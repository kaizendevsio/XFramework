---
name: xframework-new-service
description: Create or refactor XFramework service classes following VSA, Result, EF Core, caching, logging, and OpenTelemetry conventions. Use when adding service logic or improving service quality.
---

# XFramework Service Creation

Create or refactor an XFramework service following the project service-layer standards.

## When To Use

Use this skill when:
- The user asks to create or refactor a service.
- Endpoint logic needs to move into a service.
- A service must be updated for VSA, Result, EF Core, caching, logging, or OpenTelemetry conventions.

## References

- `docs/solutions/conventions/xframework-best-practices.md`, sections 5, 6, 8, 9, and 10.
- `src/Modules/XFramework.Inventario/Inventario.Api/Services/ProductService.cs` when present.

## Rules

- Prefer primary constructors for DI.
- Public async methods accept and propagate `CancellationToken ct`.
- Public methods return `Result<T>` or `Result` for expected outcomes.
- Keep HTTP concerns out of services.
- Use structured logging templates, not string interpolation.
- Add OpenTelemetry activity spans/tags when the surrounding code uses them.
- Use `AsNoTracking` and projections for read paths when possible.
- Use read-through caching and invalidate specific/list keys on writes where caching applies.
- Use cache keys in `{module}:{entity}:{identifier}` form and include tenant IDs for tenant-specific data.

## Workflow

1. Read the target module's existing services and installers.
2. Preserve existing contracts unless the user explicitly asks for a breaking change.
3. Implement the smallest service change that satisfies the behavior.
4. Register the service if needed.
5. Run a build or targeted test when feasible.

Report design choices and verification.
