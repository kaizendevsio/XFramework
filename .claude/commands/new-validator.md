# Create a FluentValidation Validator

You are writing a FluentValidation validator for an XFramework request type.

## Context
Read `docs/standards/xframework-best-practices.md` section 7 (Validation).

## Arguments
$ARGUMENTS should specify: the request type to validate (e.g., "CreateProductRequest" or the endpoint path).

## Steps

1. **Read the request type** to understand its properties
2. **Read an existing validator** for reference: `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/Create/CreateProductValidator.cs`
3. **Create the validator** following the pattern

## Validator Template

```csharp
namespace [Module].Api.Features.[Entity].[Action];

public class [Action][Entity]Validator : AbstractValidator<[Action][Entity]Endpoint.Request>
{
    public [Action][Entity]Validator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("[Entity] name is required")
            .MaximumLength(200).WithMessage("[Entity] name must not exceed 200 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero");

        // Conditional validation
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
            .When(x => x.Description is not null);
    }
}
```

## Rules to Enforce
- **One validator per request type** — never combine
- **Focus on input shape only** — format, length, required, range. Business rules belong in the service
- **Clear, user-facing error messages** — `"Product name is required"`, not `"Validation failed"`
- **Use `.When()` for conditional rules** on optional properties
- **Common validations:**
  - `NotEmpty()` for required strings and GUIDs
  - `MaximumLength()` for strings
  - `GreaterThan(0)` for positive numbers
  - `GreaterThanOrEqualTo(0)` for non-negative numbers
  - `EmailAddress()` for email fields
  - `Matches(regex)` for specific formats (phone numbers, etc.)
  - `IsInEnum()` for enum values
- **Do NOT inject services** into validators unless absolutely necessary (e.g., uniqueness check)
- **Register via assembly scanning** in ServicesInstaller: `AddValidatorsFromAssemblyContaining<Program>()`
- **File-scoped namespace**
- **Constructor — no async rules in constructor.** Use `MustAsync` only when database check is unavoidable
