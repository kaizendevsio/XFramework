---
description: Create a new VSA feature
agent: build
---

# Create a New VSA Feature

Create a new Vertical Slice Architecture feature in XFramework.

Arguments: `$ARGUMENTS` should specify module name, feature/entity name, and operations to scaffold, such as `Wallets Wallet Create Get Update Delete`.

Use `docs/solutions/conventions/xframework-best-practices.md`, `docs/solutions/conventions/xframework-vsa-agent-playbook.md`, and the Inventario reference implementation under `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/` when present.

Target structure:

```text
[Module].Api/Features/[FeatureGroup]/
├── [FeatureGroup]Endpoints.cs
├── [Action]/
│   ├── Endpoint.cs
│   └── [Action][Entity]Validator.cs
└── Shared/
    └── [Entity]Response.cs
```

Rules:
- Use file-scoped namespaces.
- Use static endpoint classes and thin handlers.
- Validate before service calls.
- Use `TypedResults` and union return types.
- Pass `CancellationToken ct` through.
- Map `Result<T>` to HTTP with pattern matching.
- Use response records with `From()` factories when appropriate.
- Keep aggregators as pure wiring.

After changes, update registration and run a build or narrow test if feasible.
