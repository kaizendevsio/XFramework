---
description: Create a new VSA feature
agent: build
---

# Create a New VSA Feature

Create a new Vertical Slice Architecture feature in XFramework.

Arguments: `$ARGUMENTS` should specify module name, feature/entity name, and operations to scaffold, such as `Wallets Wallet Create Get Update Delete`.

Use `docs/solutions/conventions/xframework-best-practices.md`, `docs/solutions/conventions/xframework-vsa-agent-playbook.md`, and `docs/solutions/conventions/xframework-feature-surface-map.md`. For generated endpoint behavior, consult `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md` and `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md`. Use the Inventario reference implementation under `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/` when present.

Target structure, adjusted to the owning module:

```text
src/Modules/XFramework.[Module]/[Module].Api/
`-- Features/
    `-- [FeatureGroup]/
        |-- [Action]/
        |   |-- Endpoint.cs
        |   `-- [Action][Entity]Validator.cs
        `-- Shared/
            `-- [Entity]Response.cs
```

Use nearby feature folders in the same module as the naming authority.

Rules:
- Follow the canonical VSA rules in `docs/solutions/conventions/xframework-vsa-agent-playbook.md`; do not restate or invent local variants.
- Use generated Minimal API endpoint attributes (`[MapGet]`, `[MapPost]`, `[MapPut]`, `[MapPatch]`, `[MapDelete]`) for generator-discovered handlers.
- Return `Result<T>` from generated handlers; reserve `TypedResults` and union return types for fully manual endpoints.
- Add FluentValidation validators only when request validation is required. Name them `[Action][Entity]Validator`; do not inject `IValidator<TRequest>` into generated `[Map*]` handler signatures.
- Add `[BoltHandler]` only when the request implements `IBoltRequest<TRequest, TResponse>`.
- Confirm the module maps generated endpoints with `app.MapGeneratedEndpoints()`.

After changes, update only required DI/validator registrations, confirm generated endpoint mapping exists, and run a build or narrow test when feasible.
