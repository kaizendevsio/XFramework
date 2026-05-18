---
description: Create a FluentValidation validator
agent: build
---

# Create a FluentValidation Validator

Create or update a FluentValidation validator for an XFramework request type.

Arguments: `$ARGUMENTS` should identify the request type or endpoint path.

Use `docs/solutions/conventions/xframework-best-practices.md` section 7 as the governing standard and use existing validators such as `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Create/CreateProductValidator.cs` as references when present.

Rules:
- One validator per request type.
- Validate input shape only: required values, length, format, range, enum membership.
- Keep business rules in services, not validators.
- Use clear user-facing messages.
- Use `.When()` for optional conditional rules.
- Avoid service injection unless a database-backed validation is unavoidable.
- Use file-scoped namespaces.
- Register through assembly scanning with `AddValidatorsFromAssemblyContaining<Program>()`.

After changes, report files touched and any validation assumptions.
