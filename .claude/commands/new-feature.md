# Create a New VSA Feature

You are creating a new Vertical Slice Architecture feature in XFramework. Follow these standards exactly.

## Context
Read the best practices document at `docs/standards/xframework-best-practices.md` for full reference. The reference implementation is at `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/`.

## Arguments
$ARGUMENTS should specify: module name, feature/entity name, and which operations to scaffold (Create, Get, GetList, Update, Delete, or a custom action like Transfer).

## Steps

1. **Identify the target module** from the argument (e.g., Wallets, IdentityServer, Inventario)
2. **Read the module's existing code** — Program.cs, ServicesInstaller.cs, any existing Features/ and Services/
3. **Read the Inventario reference** — look at `src/Modules/XFramework.Inventario/Inventario.Api/Features/Products/` for the exact pattern
4. **Create the feature folder structure:**
   ```
   [Module].Api/Features/[FeatureGroup]/
   ├── [FeatureGroup]Endpoints.cs       # Aggregator
   ├── [Action]/
   │   ├── Endpoint.cs                  # Minimal API handler
   │   └── [Action][Entity]Validator.cs # FluentValidation (for Create/Update)
   └── Shared/
       └── [Entity]Response.cs          # Response DTO with From() factory
   ```

## Endpoint Pattern (MUST follow)

```csharp
namespace [Module].Api.Features.[Entity].[Action];

public static class [Action][Entity]Endpoint
{
    // Request record can be inline if < 10 properties
    public record Request { public required string Name { get; init; } }

    public static void Map[Action][Entity](this IEndpointRouteBuilder app)
    {
        app.Map[HttpVerb]("/api/[entities]", Handle)
            .WithName("[Action][Entity]")
            .WithTags("[Entities]")
            .WithDescription("[Description]")
            .Produces<[Response]>(StatusCodes.Status2xx)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<[SuccessResult], ValidationProblem, ProblemHttpResult>> Handle(
        Request request,
        IValidator<Request> validator,
        [Entity]Service service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return TypedResults.ValidationProblem(validation.ToDictionary());

        var result = await service.[Method]Async(request, ct);

        return result switch
        {
            { IsSuccess: true } => TypedResults.[SuccessResult](...),
            _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
        };
    }
}
```

## Rules to Enforce
- File-scoped namespaces
- `CancellationToken ct` on every async method
- TypedResults only (never anonymous objects)
- Results<T1, T2, ...> union return types
- Thin handlers — validate, call service, map result. No business logic.
- Pattern matching for Result<T> → HTTP response mapping
- Use `required` keyword on request record properties that are mandatory
- Response records with static `From()` factory method
- One public entry point per Endpoint.cs
- Aggregator is pure wiring — no logic
- Register in Program.cs via `app.Map[Entity]Endpoints();`
