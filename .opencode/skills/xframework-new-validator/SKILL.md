---
name: xframework-new-validator
description: Create or update FluentValidation validators for XFramework VSA requests. Use when adding request validation, fixing validator gaps, or scaffolding endpoint validators.
---

# XFramework Validator Creation

Create or update FluentValidation validators for XFramework request types.

## When To Use

Use this skill when:
- The user asks for a new validator.
- A new VSA endpoint request is added and needs validation.
- Review finds missing or weak request validation.

## References

- `docs/solutions/conventions/xframework-best-practices.md`, validation section.
- Existing validators such as `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Create/CreateProductValidator.cs` when present.

## Rules

- One validator per request type.
- Validate input shape only: required values, length, format, range, enum membership.
- Keep business rules in services, not validators.
- Use clear user-facing messages.
- Use `.When()` for optional conditional rules.
- Avoid service injection unless a database-backed validation is unavoidable.
- Use file-scoped namespaces.
- Register through assembly scanning with `AddValidatorsFromAssemblyContaining<Program>()`.

## Workflow

1. Read the request type and endpoint context.
2. Read a nearby validator for naming and style.
3. Add or update the validator next to the endpoint/request it validates.
4. Verify registration is already covered by assembly scanning.
5. Run a narrow build/test when feasible.

Report files touched and any validation assumptions.
