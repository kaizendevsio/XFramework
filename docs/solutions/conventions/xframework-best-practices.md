---
title: "XFramework Best Practices and Standards"
date: 2026-05-21
category: conventions
module: XFramework
problem_type: convention
component: development_workflow
severity: high
applies_when:
  - "Creating, refactoring, or reviewing XFramework features against VSA, endpoint, service, validation, data-access, caching, observability, testing, security, and style conventions"
tags: [vsa, standards, conventions, endpoints, services, testing]
---

# XFramework Best Practices & Standards

> **Version:** 2.0
> **Last Updated:** 2026-05-21
> **Target:** .NET 10 / C# 14
> **Architecture:** Feature-Centric Vertical Slice Architecture (VSA)
> **Purpose:** Canonical reference for all code written in the XFramework codebase. Every new feature, refactor, and code review should be evaluated against this document.

---

## Table of Contents

1. [Architecture: Vertical Slice Architecture](#1-architecture-vertical-slice-architecture)
2. [Project & File Structure](#2-project--file-structure)
3. [C# 14 & .NET 10 Idioms](#3-c-14--net-10-idioms)
4. [Endpoint Design (Minimal API)](#4-endpoint-design-minimal-api)
5. [Service Layer](#5-service-layer)
6. [Result Pattern](#6-result-pattern)
7. [Validation](#7-validation)
8. [Data Access (EF Core)](#8-data-access-ef-core)
9. [Caching](#9-caching)
10. [Observability](#10-observability)
11. [Error Handling](#11-error-handling)
12. [Testing](#12-testing)
13. [Security](#13-security)
14. [Dependency Injection & Registration](#14-dependency-injection--registration)
15. [Performance](#15-performance)
16. [Code Style & Conventions](#16-code-style--conventions)

---

## 1. Architecture: Vertical Slice Architecture

### 1.1 Core Principle

Organize code by **feature**, not by **technical layer**. Each feature is a self-contained vertical slice that owns its endpoint, request/response types, validation, and orchestration logic.

### 1.2 Rules

- **No cross-feature imports.** Features must not reference types from other features. Shared types go in a `Shared/` folder within the feature group, or in the `Domain.Shared` project if needed cross-module.
- **No "Services" layer as an architectural boundary.** Services are implementation details owned by the feature slice. A service file that serves multiple features lives in `Services/` at the module root, but it is still just a helper — not a layer.
- **Features are defined by behavior, not entities.** A "Transfer" feature in Wallets is its own slice — it does not piggyback on a generic "Update Wallet" slice.
- **One public entry point per slice.** Each feature folder has exactly one `Endpoint.cs` that defines the HTTP contract. Internal helpers are `internal` or `private`.
- **Prefer duplication over wrong abstraction.** If two features have similar-looking code, keep them separate unless the shared behavior is truly stable and well-understood. Extract only when you see three or more identical patterns.

### 1.3 When Shared Code Is Acceptable

- **Domain entities and contracts** — `Domain.Shared` project
- **Cross-cutting infrastructure** — `XFramework.Core` (Result<T>, caching, logging extensions)
- **DbContext and interceptors** — `XFramework.Domain`
- **Response DTOs shared within a feature group** — `Features/[Entity]/Shared/`
- **Utility extension methods** — only when genuinely reusable across 3+ call sites

---

## 2. Project & File Structure

### 2.1 Module Layout

```
[Module].Api/
├── Features/
│   └── [FeatureGroup]/
│       ├── [FeatureGroup]Endpoints.cs    # Optional aggregator for fully manual mappings
│       ├── Create/
│       │   ├── Endpoint.cs               # Single static class, single handler
│       │   └── CreateValidator.cs         # FluentValidation rules
│       ├── Get/
│       │   └── Endpoint.cs
│       ├── GetList/
│       │   └── Endpoint.cs
│       ├── Update/
│       │   ├── Endpoint.cs
│       │   └── UpdateValidator.cs
│       ├── Delete/
│       │   └── Endpoint.cs
│       └── Shared/
│           └── [FeatureGroup]Response.cs  # Response DTO shared within group
├── Services/
│   ├── I[Service].cs                     # Interface (only if needed for testing/DI)
│   └── [Service].cs                      # Implementation using Result<T>
├── Entities/                             # Module-specific EF entity configs (if any)
├── Installers/
│   └── ServicesInstaller.cs              # DI registration
├── GlobalUsings.cs
└── Program.cs                            # XApplication.Configure<Program>() entry
```

### 2.2 Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Feature folder | PascalCase, action verb or noun | `Create/`, `GetList/`, `Transfer/` |
| Endpoint class | `{Action}{Entity}Endpoint` | `CreateProductEndpoint` |
| Generated handler method | `Handle` with `[Map*]` | `[MapPost] public static Task<Result<ProductResponse>> Handle(...)` |
| Validator | `{Action}{Entity}Validator` | `CreateProductValidator` |
| Response DTO | `{Entity}Response` | `ProductResponse` |
| Request record | `{Action}{Entity}Request` | `CreateProductRequest` |
| Service | `{Entity}Service` / `{Domain}Service` | `ProductService`, `WalletService` |
| Aggregator | `{Entity}Endpoints` only for fully manual mappings | `ProductEndpoints` |

### 2.3 File Rules

- **One type per file.** Exception: request/response records can live inside `Endpoint.cs` if they are small (< 10 properties) and used only by that endpoint.
- **Keep endpoint files short.** An `Endpoint.cs` should be < 80 lines. If the handler logic grows beyond that, the complexity belongs in the service.
- **No `Controllers/` folders.** All HTTP endpoints use Minimal API. Legacy controllers must be migrated.

---

## 3. C# 14 & .NET 10 Idioms

### 3.1 Use Modern Language Features

```csharp
// ✅ Use 'field' keyword for semi-auto properties (C# 14)
public string Name
{
    get => field;
    set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
}

// ✅ Use primary constructors for DI (C# 12+, standard practice)
public class ProductService(AppDbContext db, ICacheService cache, ILogger<ProductService> logger)
{
    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct) { ... }
}

// ✅ Use collection expressions (C# 12+)
List<string> tags = ["dotnet", "csharp", "vsa"];
int[] ids = [1, 2, 3];

// ✅ Use required members for DTOs
public record CreateProductRequest
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string? Description { get; init; }
}

// ✅ Use raw string literals for multi-line strings
var sql = """
    SELECT p."Id", p."Name"
    FROM "Products" p
    WHERE p."IsDeleted" = false
    """;

// ✅ Use pattern matching exhaustively
return result switch
{
    { IsSuccess: true } => TypedResults.Ok(result.Data),
    { StatusCode: 404 } => TypedResults.NotFound(),
    { StatusCode: 409 } => TypedResults.Conflict(result.Message),
    _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
};

// ✅ Use file-scoped namespaces everywhere
namespace Inventario.Api.Features.Products.Create;

// ✅ Use global using directives in GlobalUsings.cs
global using XFramework.Core.Patterns;
global using FluentValidation;
```

### 3.2 Records vs Classes

| Use | Type |
|-----|------|
| Request/Response DTOs | `record` (immutable by default, value equality) |
| Domain entities | `class` (mutable, identity-based, EF tracked) |
| Configuration objects | `record` or `class` with `required` properties |
| Result types | `record` (immutable, pattern-match friendly) |
| Services | `class` with primary constructor |

### 3.3 .NET 10 Specific

- **Know the current cache implementation** - XFramework currently uses the custom `HybridCacheService` for L1 memory + optional L2 Redis caching. .NET 10 also ships `Microsoft.Extensions.Caching.Hybrid.HybridCache`; evaluate it before future refactors, but do not document it as already adopted.
- **Use built-in OpenAPI** — .NET 10 supports `Microsoft.AspNetCore.OpenApi` natively. Consider replacing Swashbuckle with the built-in OpenAPI document generation.
- **Use `TypedResults`** consistently — .NET 10 Minimal APIs have full `TypedResults` support with improved OpenAPI metadata inference.
- **EF Core 10** — Leverage compiled queries for hot paths, improved `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for bulk operations, and improved LINQ translation.
- **Update NuGet packages to 10.x** — All `Microsoft.AspNetCore.*` and `Microsoft.EntityFrameworkCore.*` packages should match the target framework version (10.0.x).

---

## 4. Endpoint Design (Minimal API)

### 4.1 Endpoint Structure

Most manual VSA endpoints use source-generator attributes. `BoltHandlerGenerator` emits the Minimal API adapter from `[MapPost]`, `[MapGet]`, `[MapPut]`, `[MapPatch]`, or `[MapDelete]`; it emits a Bolt `IBoltHandler` when `[BoltHandler]` is also present.

```csharp
namespace Module.Api.Features.Entity.Create;

public static class CreateEntityEndpoint
{
    [MapPost("/api/entities", Tags = ["Entities"], Summary = "Create entity")]
    [BoltHandler]
    public static Task<Result<EntityResponse>> Handle(
        Request request,
        EntityService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);
}

public sealed record Request(string Name) : IBoltRequest<Request, Result<EntityResponse>>;
```

For method-level `[Map*]` handlers, do not add `IValidator<TRequest>` to the handler signature. `BoltHandlerGenerator` detects a concrete `AbstractValidator<TRequest>` at compile time and the generated REST adapter injects `IValidator<TRequest>`, runs `ValidateAsync`, and returns `TypedResults.ValidationProblem(...)` before calling `Handle`.

### 4.2 Endpoint Rules

- **Always use `CancellationToken ct`** as the last parameter — honor client disconnection.
- **Generated `[Map*]` REST endpoints auto-validate.** Add a concrete `AbstractValidator<TRequest>` for the request type and register validators with assembly scanning; do not inject `IValidator<TRequest>` into the generated handler method.
- **Manual validation is only for non-generated endpoints or custom flows.** If an endpoint is mapped manually because it needs custom binding or custom `IResult` output, inject `IValidator<TRequest>` there and return `TypedResults.ValidationProblem(...)` yourself.
- **Return `Result<T>` from generated handlers.** The generated REST adapter converts successful and failed results to HTTP responses.
- **Use `TypedResults` and `Results<T1, T2, ...>` only for manual endpoints that are not generated from `[Map*]` attributes.**
- **Map service `Result<T>` to HTTP via pattern matching in manual endpoints.** Generated `[Map*]` handlers return `Result<T>` and let the generated adapter translate it.
- **Keep handlers thin.** A generated handler receives the request and services, calls the service, and returns `Result<T>`. Business logic stays out of endpoints.
- **Use `WithDescription()` over `WithOpenApi()`** when you only need summary/description — simpler API.
- **Use route constraints** for type safety: `/api/products/{id:guid}`, `/api/orders/{page:int}`.
- **Pagination defaults:** page=1, pageSize=20, maxPageSize=100. These should be configurable per endpoint.
- **Use `[BoltHandler]` only when the first parameter implements `IBoltRequest<TRequest, TResponse>`.** Without that contract the Bolt handler is not generated.

### 4.3 Registration Pattern

```csharp
// Program.cs in an API module
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = (WebApplication)builder.Build();
app.MapGeneratedEndpoints();

// Keep explicit manual mappings when route/header binding or custom IResult output is required.
GetWalletEndpoint.Map(app);
```

- `GeneratedEndpointRoutes.g.cs` is created from attributed feature handlers.
- `EndpointDiscoveryExtensions.MapGeneratedEndpoints()` discovers generated endpoint classes whose names end with `Endpoints`.
- Entity-generated CRUD endpoints are generated from `[GenerateEndpoints]`; manual VSA endpoints are generated from method-level `[Map*]` attributes.
- Do not manually call generated `Map{Action}{Entity}` methods from `Program.cs`; `GeneratedEndpointRoutes.g.cs` calls them from `MapGeneratedEndpoints()`.
- Keep `{Entity}Endpoints` aggregator files only for explicit manual endpoint mappings that are not generated from `[Map*]` attributes.
- See `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md` for registration mechanics and `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md` for entity-generated CRUD options.

---

## 5. Service Layer

### 5.1 Service Design

```csharp
public class ProductService(
    AppDbContext db,
    ICacheService cache,
    ILogger<ProductService> logger)
{
    // Every public method returns Result<T> or Result
    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // 1. Try cache
        // 2. Query database
        // 3. Populate cache
        // 4. Return Result
    }
}
```

### 5.2 Service Rules

- **Use primary constructors** for dependency injection. No manual field assignment boilerplate.
- **Every public method returns `Result<T>` or `Result`.** No raw exceptions crossing the boundary.
- **Accept `CancellationToken ct`** on every async method and pass it through to EF, HTTP clients, etc.
- **Services own business logic.** Request validation is done by generated adapters or manual endpoints; the service assumes input is valid.
- **Services should not know about HTTP.** No `HttpContext`, `StatusCodes`, or `TypedResults` in services. They return domain-level results; the endpoint translates to HTTP.
- **One service per feature domain**, not per entity. `WalletService` handles wallets, funds, transfers, and conversions. Don't create `FundsService`, `TransferService` separately unless the complexity warrants it.
- **Use `ConfigureAwait(false)`** in library/service code if the service is in a shared library project. For ASP.NET API projects, this is not needed.
- **Log at the right level:**
  - `LogDebug` — Cache hits, query details
  - `LogInformation` — Successful operations (entity created, transferred)
  - `LogWarning` — Expected failures (not found, validation, insufficient funds)
  - `LogError` — Unexpected exceptions (database errors, external service failures)

### 5.3 Interface Decision

- **Prefer concrete service injection** when the service has only one implementation and no testing mock is needed.
- **Use interfaces** when:
  - The service is injected cross-module (e.g., `ISmsGatewayServiceWrapper`)
  - You need to mock it in unit tests
  - Multiple implementations exist (e.g., different payment gateways)

---

## 6. Result Pattern

### 6.1 Usage Rules

```csharp
// ✅ Return typed results
public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct)
{
    var product = await db.Products.FindAsync([id], ct);
    if (product is null)
        return Result<Product>.NotFound($"Product {id} not found");

    return Result<Product>.Success(product);
}

// ✅ Non-generic for void operations
public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
{
    // ...
    return Result.Success();
}

// ✅ Map Result to HTTP in endpoints using pattern matching
return result switch
{
    { IsSuccess: true } => TypedResults.Ok(result.Data),
    { StatusCode: 404 } => TypedResults.NotFound(),
    _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
};
```

### 6.2 Rules

- **Never throw exceptions for expected failures.** Use `Result.Failure()`, `Result.NotFound()`, etc.
- **Reserve exceptions for truly exceptional situations** — database connection lost, null reference bugs, out of memory.
- **Include meaningful messages.** `"Product {id} not found"` is better than `"Not found"`.
- **Do not leak internal details.** `"Database query failed"` is acceptable; the full SQL or stack trace is not.
- **Use the specific factory method** that matches the HTTP semantics: `NotFound()` → 404, `Conflict()` → 409, etc.

---

## 7. Validation

### 7.1 FluentValidation Pattern

```csharp
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required");
    }
}
```

### 7.2 Validation Rules

- **Validate at the endpoint boundary** — before calling the service.
- **Use `FluentValidation.ToDictionary()`** to convert errors for `TypedResults.ValidationProblem()`.
- **One validator per request type.** Do not combine validators.
- **Keep validators focused on input shape** — format, length, required fields. Business rules (e.g., "wallet must have sufficient funds") belong in the service.
- **Register validators via assembly scanning:**
  ```csharp
  builder.Services.AddValidatorsFromAssemblyContaining<Program>();
  ```
- **Do not inject services into validators** unless absolutely necessary (e.g., uniqueness check). Prefer deferring business validation to the service layer.

---

## 8. Data Access (EF Core)

Detailed authority: [`ef-core-data-access-patterns.md`](ef-core-data-access-patterns.md). Keep this section as the short checklist; use the dedicated guide for `AppDbContext` discovery, module configuration placement, migrations, tests, and local/remote `IDataContext` behavior.

### 8.1 Global Defaults

These are set in `XDbContext`, module `DbInstaller.cs` files, and `AuditInterceptor` and apply automatically:

- `QueryTrackingBehavior.NoTracking` - all queries are read-only by default
- `QuerySplittingBehavior.SplitQuery` - avoids cartesian explosion with joins
- Global query filter: `ISoftDeletable` -> excludes `IsDeleted == true`
- Global query filter: `IHasTenantId` -> filters by current tenant
- `AuditInterceptor` -> auto-populates `CreatedAt` and `ModifiedAt`
- `XDbContext.OnBeforeSaveChanges()` -> handles soft-delete conversion, default values, and tenant validation

### 8.2 Query Rules

```csharp
// ✅ Read operations — NoTracking is default, be explicit for clarity on hot paths
var product = await db.Products
    .AsNoTracking()
    .Include(p => p.Category)
    .FirstOrDefaultAsync(p => p.Id == id, ct);

// ✅ Write operations — attach and track explicitly
db.Products.Update(product);
await db.SaveChangesAsync(ct);

// ✅ Bulk operations — use ExecuteUpdateAsync/ExecuteDeleteAsync (EF Core 7+)
await db.Products
    .Where(p => p.CategoryId == categoryId)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsAvailable, false), ct);

// ✅ Projections — select only what you need
var response = await db.Products
    .Where(p => p.Id == id)
    .Select(p => new ProductResponse(p.Id, p.Name, p.Price))
    .FirstOrDefaultAsync(ct);

// ❌ Don't load full entities when you only need a few fields
// ❌ Don't use .ToList() before .Where() — filter in the database
// ❌ Don't call SaveChanges inside a loop — batch operations
```

### 8.3 Rules

- **Use projections** (`.Select()`) for read endpoints whenever possible. Reduces memory and network overhead.
- **Use `FindAsync` for single-entity lookup by primary key** — it checks the context cache first.
- **Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync`** for bulk operations instead of loading + iterating.
- **Never call `SaveChangesAsync` in a loop.** Batch all changes and call once.
- **Use `IQueryable<T>` for building queries**, not `IEnumerable<T>`. Filters must execute in the database.
- **Favor `.AsSplitQuery()`** when using multiple `Include()` calls to avoid cartesian explosion.
- **Use `.IgnoreQueryFilters()`** sparingly and only for admin operations that explicitly need soft-deleted or cross-tenant data.
- **Mark entity classes as `sealed`** when they are not intended for inheritance — EF Core can optimize sealed types.

### 8.4 Soft Delete

Soft delete is handled by `XDbContext.OnBeforeSaveChanges()`. Delete entities through EF/context behavior (`Remove` / `EntityState.Deleted`), then save changes. When `EntityState.Deleted` is detected:

- Sets `IsDeleted = true` and `DeletedAt = DateTime.UtcNow`
- Changes state to `Modified` so no physical DELETE is sent
- Global query filters exclude soft-deleted records from normal queries

**Do not manually set `IsDeleted = true` in services.** Rely on the context behavior so `DeletedAt`, state conversion, and filters stay consistent.

### 8.5 Multi-Tenancy

Tenant isolation is enforced at the DbContext level via global query filter on `IHasTenantId`. The current tenant is extracted from `HttpContext` claims.

- **Always ensure TenantId is set** on new tenant-owned entities. `XDbContext.OnBeforeSaveChanges()` validates that `BaseModel.TenantId` is not null or `Guid.Empty`; `AuditInterceptor` does not set or validate tenant IDs.
- **Never bypass tenant filters** in user-facing code. Only admin/system endpoints may use `.IgnoreQueryFilters()`.

---

## 9. Caching

Detailed authority: [`../best-practices/xframework-caching-strategy.md`](../best-practices/xframework-caching-strategy.md). Keep cache-key, invalidation, Redis, client-cache, and generated endpoint cache metadata details there.

### 9.1 Strategy

The framework uses a hybrid caching approach:
- **L1 (Memory):** In-process `IMemoryCache`, sub-millisecond latency
- **L2 (Redis):** Distributed `IDistributedCache`, shared across instances

### 9.2 .NET 10 HybridCache Consideration

.NET 10 provides `Microsoft.Extensions.Caching.Hybrid.HybridCache` with built-in:
- Stampede protection (only one factory call per key under concurrent requests)
- Serialization abstraction
- Tag-based invalidation
- L1 + L2 orchestration

**Recommendation:** Evaluate migrating to the built-in `HybridCache` to reduce custom code and benefit from framework-level optimizations. The custom `HybridCacheService` can be kept as a wrapper if additional features (statistics tracking, graceful degradation) are needed.

### 9.3 Caching Rules

- **Cache reads, not writes.** Cache `GetById` and `GetList` results. Invalidate on Create/Update/Delete.
- **Use prefix-based invalidation** for list caches: `products:list:*` invalidated when any product changes.
- **Set reasonable TTLs.** Default: 5-10 minutes for entity caches. Never cache indefinitely.
- **Graceful degradation is mandatory.** If Redis is unavailable, the app must continue working with L1 only.
- **Do not cache user-specific or tenant-specific data in shared L2** without including the tenant ID in the cache key.
- **Cache key format:** `{module}:{entity}:{identifier}` — e.g., `inventario:product:{guid}`, `wallets:list:credential:{guid}`.

---

## 10. Observability

Current logging pipeline: ZLogger through `AddXFrameworkLogging()`, with optional Seq output. OpenTelemetry tracing and metrics are configured by `InstallOpenTelemetry()`. Historical Serilog references are migration context only.

### 10.1 Structured Logging

```csharp
// ✅ Use structured logging with semantic properties
logger.LogInformation("Product {ProductId} created by {UserId}", product.Id, userId);

// ✅ Use LoggerMessage source generators for high-performance hot paths
[LoggerMessage(Level = LogLevel.Information, Message = "Product {ProductId} created")]
partial void LogProductCreated(Guid productId);

// ❌ Don't use string interpolation in log templates
logger.LogInformation($"Product {product.Id} created");  // BAD — defeats structured logging
```

### 10.2 OpenTelemetry

- **Activity/Span per service operation:**
  ```csharp
  using var activity = ActivitySource.StartActivity("ProductService.GetById");
  activity?.SetTag("product.id", id.ToString());
  ```
- **Metrics for business operations:**
  - Counters: `products.created`, `wallets.transfers`
  - Histograms: `product.query.duration`, `wallet.transfer.duration`
- **Correlation IDs** propagated via `UseCorrelationId()` middleware.

### 10.3 Rules

- **Every service method should log its entry and exit** at appropriate levels.
- **Use semantic tag names** in spans: `entity.id`, `entity.type`, `operation.name`.
- **Record exceptions in spans:** `activity?.SetStatus(ActivityStatusCode.Error, ex.Message)`.
- **Do not log sensitive data** — passwords, tokens, PII. Redact or omit.

---

## 11. Error Handling

### 11.1 Strategy

```
Endpoint (HTTP boundary)
  └─ Validates input → ValidationProblem (400)
  └─ Calls Service → maps Result<T> to HTTP response
       └─ Service catches known failures → Result.Failure / NotFound / Conflict
       └─ Service lets unknown exceptions propagate → caught by global handler → 500
```

### 11.2 Rules

- **Use try-catch in services** only for expected external failures (database timeout, HTTP call failure). Return `Result.Failure()`.
- **Let unexpected exceptions propagate.** A global exception handler middleware will catch and log them, returning a generic 500.
- **Never swallow exceptions** (`catch { }` with no body). Always log or return a meaningful Result.
- **Problem Details (RFC 9457)** is the standard error response format. Use `TypedResults.Problem()` which generates this automatically.
- **Do not return stack traces** in non-development environments.

---

## 12. Testing

### 12.1 Test Organization

```
src/Tests/
├── XFramework.Core.Tests/
│   ├── Patterns/
│   │   └── ResultTests.cs
│   └── Services/
│       └── Caching/
│           └── HybridCacheServiceTests.cs
├── [Module].Tests/
│   ├── Services/
│   │   └── ProductServiceTests.cs
│   └── Features/
│       └── Products/
│           └── CreateProductEndpointTests.cs
```

### 12.2 Rules

- **Framework:** NUnit + FluentAssertions + Moq (or NSubstitute).
- **Unit tests** for services: mock DbContext, test business logic, verify Result outcomes.
- **Integration tests** for endpoints: use `WebApplicationFactory<Program>`, test full request → response pipeline.
- **Test naming:** `MethodName_Scenario_ExpectedResult` — e.g., `GetByIdAsync_ProductNotFound_ReturnsNotFoundResult`.
- **Assert on Result properties**, not just booleans:
  ```csharp
  result.IsSuccess.Should().BeFalse();
  result.StatusCode.Should().Be(404);
  result.Message.Should().Contain("not found");
  ```
- **Test edge cases:** null inputs, empty collections, max pagination, concurrent access.
- **Minimum coverage target:** 80% on service layer code. 100% on Result pattern and core infrastructure.

---

## 13. Security

### 13.1 Rules

- **Validate all external input** at the endpoint boundary with FluentValidation.
- **Never trust client-provided tenant IDs.** Extract from authenticated claims only.
- **Use parameterized queries** (EF Core does this by default — never build raw SQL from user input).
- **Sanitize log output** — no passwords, tokens, connection strings, or PII in logs.
- **Use HTTPS in all environments.**
- **CORS policies** must be explicit — never use `AllowAnyOrigin()` in production.
- **JWT tokens** must have reasonable expiration. Refresh tokens must be rotatable.
- **Rate limiting** on authentication endpoints to prevent brute force.
- **Do not expose internal entity IDs** in error messages returned to clients.

---

## 14. Dependency Injection & Registration

### 14.1 Service Lifetimes

| Type | Lifetime | Reason |
|------|----------|--------|
| `DbContext` | Scoped | One context per request, EF requirement |
| Business services (`ProductService`) | Scoped | May depend on scoped DbContext |
| `ICacheService` | Singleton | Thread-safe, shared across requests |
| `CachingService` (in-memory ConcurrentDict) | Singleton | State must persist across requests |
| Validators | Scoped | Default for FluentValidation DI |
| `ILogger<T>` | Singleton | Framework-managed |

### 14.2 Registration Pattern

```csharp
// In ServicesInstaller.cs
public class ServicesInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProductService>();
        services.AddValidatorsFromAssemblyContaining<Program>();
    }
}

// In Program.cs — use XApplication convention
XApplication.Configure<Program>()
    // ... framework setup
```

### 14.3 Rules

- **Register services in `ServicesInstaller.cs`**, not scattered in `Program.cs`.
- **Use assembly scanning** for validators: `AddValidatorsFromAssemblyContaining<Program>()`.
- **Prefer `AddScoped` for services** that use DbContext.
- **Never resolve scoped services from singleton scope** — this causes captive dependency bugs.

---

## 15. Performance

### 15.1 Database

- Global `AsNoTracking` for all queries (set in DbInstaller)
- Global `SplitQuery` to avoid cartesian explosion
- Use `.Select()` projections for read-only DTOs
- Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for bulk operations
- Add database indexes on frequently queried columns (foreign keys, lookup fields)
- Consider compiled queries for hot paths (frequently called, latency-sensitive)

### 15.2 Caching

- Cache hot-path reads with appropriate TTL
- Invalidate on writes using prefix-based removal
- Include tenant ID in cache keys for multi-tenant data
- Monitor cache hit rates via HybridCacheService statistics

### 15.3 API

- Response compression (Brotli + Gzip) enabled globally
- Output caching for idempotent GET endpoints with `CacheOutput("PolicyName")`
- Pagination on all list endpoints — never return unbounded collections
- Cancellation token propagation through entire call chain

### 15.4 Serialization

- Use `System.Text.Json` with cached `JsonSerializerOptions` (do not create new options per call)
- Use `MemoryPack` for high-throughput internal serialization with Bolt and inter-service payloads
- Use Bolt for new module RPC/streaming; SignalR references are historical or comparative only

---

## 16. Code Style & Conventions

### 16.1 General

- **File-scoped namespaces** everywhere
- **Nullable reference types** enabled (`#nullable enable` via project settings)
- **`global using`** statements in `GlobalUsings.cs` per project
- **No `#region` blocks** — they hide complexity instead of reducing it
- **No comments that restate the code.** Only comment the "why", not the "what"
- **`sealed` classes** by default unless inheritance is explicitly needed

### 16.2 Naming

- PascalCase: types, methods, properties, constants
- camelCase: local variables, parameters
- `_camelCase`: private fields (only when not using primary constructors)
- `I` prefix: interfaces (`ICacheService`)
- `Async` suffix: async methods (`GetByIdAsync`)
- No Hungarian notation, no abbreviations (except well-known: `Id`, `Db`, `Dto`)

### 16.3 Records & DTOs

```csharp
// ✅ Immutable request with required properties
public record CreateProductRequest
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string? Description { get; init; }
}

// ✅ Response with factory method
public record ProductResponse(Guid Id, string Name, decimal Price, string? Description)
{
    public static ProductResponse From(Product entity) =>
        new(entity.Id, entity.Name, entity.Price, entity.Description);
}
```

### 16.4 Avoid

- ❌ `var` for non-obvious types — prefer explicit types when the type isn't clear from the right-hand side
- ❌ Nested ternary expressions — use pattern matching or if/else
- ❌ `object` or `dynamic` as parameter/return types
- ❌ Empty catch blocks
- ❌ `Thread.Sleep` or `Task.Delay` for flow control
- ❌ Mutable static fields
- ❌ `public` fields (use properties)

---

## Appendix: Decision Log

| Decision | Rationale |
|----------|-----------|
| VSA over Clean Architecture layers | Reduces cognitive overhead, collocates related code, simplifies navigation |
| Minimal API over Controllers | Less ceremony, better performance, TypedResults for OpenAPI |
| Result<T> over exceptions | Explicit error handling, no hidden control flow, pattern-match friendly |
| FluentValidation over Data Annotations | Richer rules, testable, separation from domain models |
| Primary constructors over field injection | Less boilerplate, clearer dependencies, C# 14 idiomatic |
| HybridCache (L1+L2) over single-tier | Latency optimization (in-process) + consistency (distributed) |
| Global EF query filters over per-query | Eliminates entire class of bugs (forgotten tenant filter, forgotten soft-delete check) |
| ZLogger over Serilog | Single Microsoft.Extensions.Logging pipeline, ZLogger console/Seq providers, scope support, and no competing logger factory |
| PostgreSQL as primary DB | OSS, JSONB support, Npgsql performance, industry standard |
| Central Package Management | Single source of truth for NuGet versions across monorepo |
