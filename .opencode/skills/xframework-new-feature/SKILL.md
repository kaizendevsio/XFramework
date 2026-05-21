---
name: xframework-new-feature
description: Create XFramework Vertical Slice Architecture features and endpoints. Use when adding a new module feature, endpoint slice, CRUD action, custom action, or VSA folder structure.
---

# XFramework VSA Feature Creation

Create a new Vertical Slice Architecture feature in XFramework.

## When To Use

Use this skill when:
- The user asks to add a new feature or endpoint.
- A module needs a new CRUD operation or custom action.
- Existing behavior should be reshaped into VSA feature folders.

## References

- `docs/solutions/conventions/xframework-best-practices.md`
- `docs/solutions/conventions/xframework-vsa-agent-playbook.md`
- `docs/solutions/conventions/xframework-feature-surface-map.md`
- `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md`
- `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md`
- `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/` when present.

## Target Structure

```text
[Module].Api/Features/[FeatureGroup]/
|-- [Action]/
|   |-- Endpoint.cs
|   `-- [Action][Entity]Validator.cs
`-- Shared/
    `-- [Entity]Response.cs
```

## Rules

- Use file-scoped namespaces.
- Use static endpoint classes with `[Map*]` attributes and thin handlers.
- Validate before service calls.
- Return `Result<T>` from generated handlers; reserve `TypedResults` and union return types for fully manual endpoints.
- Add `[BoltHandler]` only when the request implements `IBoltRequest<TRequest, TResponse>`.
- Pass `CancellationToken ct` through.
- Use response records with `From()` factories when appropriate.
- Ensure the module maps generated endpoints with `app.MapGeneratedEndpoints()`.

## Workflow

1. Identify the target module and existing feature conventions.
2. Read nearby feature, service, validator, installer, and `Program.cs` files.
3. Create the smallest feature structure needed for the requested operation.
4. Add validator/service changes only when required.
5. Confirm generated endpoint mapping exists in module startup.
6. Run a build or narrow test when feasible.

Report files added, registration points, and verification.
