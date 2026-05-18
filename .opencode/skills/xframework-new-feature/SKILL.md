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
- `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/` when present.

## Target Structure

```text
[Module].Api/Features/[FeatureGroup]/
├── [FeatureGroup]Endpoints.cs
├── [Action]/
│   ├── Endpoint.cs
│   └── [Action][Entity]Validator.cs
└── Shared/
    └── [Entity]Response.cs
```

## Rules

- Use file-scoped namespaces.
- Use static endpoint classes and thin handlers.
- Validate before service calls.
- Use `TypedResults` and union return types.
- Pass `CancellationToken ct` through.
- Map `Result<T>` to HTTP with pattern matching.
- Use response records with `From()` factories when appropriate.
- Keep aggregators as pure wiring.

## Workflow

1. Identify the target module and existing feature conventions.
2. Read nearby feature, service, validator, installer, and `Program.cs` files.
3. Create the smallest feature structure needed for the requested operation.
4. Add validator/service changes only when required.
5. Register endpoint aggregation if needed.
6. Run a build or narrow test when feasible.

Report files added, registration points, and verification.
