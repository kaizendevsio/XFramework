---
description: Create a new VSA feature
agent: build
---

# Create a New VSA Feature

Create a new Vertical Slice Architecture feature in XFramework.

Arguments: `$ARGUMENTS` should specify module name, feature/entity name, and operations to scaffold, such as `Wallets Wallet Create Get Update Delete`.

Use `docs/solutions/conventions/xframework-best-practices.md`, `docs/solutions/conventions/xframework-vsa-agent-playbook.md`, and `docs/solutions/conventions/xframework-feature-surface-map.md`. For generated endpoint behavior, consult `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md` and `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md`. Use the Inventario reference implementation under `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/` when present.

Target structure:

```text
[Module].Api/Features/[FeatureGroup]/
|-- [Action]/
|   |-- Endpoint.cs
|   `-- [Action][Entity]Validator.cs
`-- Shared/
    `-- [Entity]Response.cs
```

Rules:
- Use file-scoped namespaces.
- Use static endpoint classes with `[Map*]` attributes and thin handlers.
- Validate before service calls.
- Return `Result<T>` from generated handlers; reserve `TypedResults` and union return types for fully manual endpoints.
- Add `[BoltHandler]` only when the request implements `IBoltRequest<TRequest, TResponse>`.
- Pass `CancellationToken ct` through.
- Use response records with `From()` factories when appropriate.
- Ensure the module maps generated endpoints with `app.MapGeneratedEndpoints()`.

After changes, update registration and run a build or narrow test if feasible.
