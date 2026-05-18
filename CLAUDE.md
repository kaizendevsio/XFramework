# XFramework Knowledge Base

This document is the authoritative reference for AI agents working on XFramework. It covers architecture, conventions, and step-by-step guides for common tasks.

## Architecture Overview

XFramework is a .NET 10 / C# 14 modular enterprise framework using:
- **Vertical Slice Architecture (VSA)** — features organized by domain, not by layer
- **StreamFlow** — custom SignalR-based binary RPC transport (MessagePack + MemoryPack)
- **Source Generators** — compile-time code generation for endpoints, StreamFlow handlers, and service wrappers
- **Central Package Management** — all NuGet versions in `Directory.Packages.props`

Documentation uses Compound Engineering paths:
- `docs/solutions/` — searchable documented solutions, conventions, architecture patterns, tooling decisions, workflow issues, and best practices, organized by category with YAML frontmatter (`module`, `problem_type`, `component`, `severity`, `tags`). Relevant when implementing, debugging, or making decisions in documented areas.
- `docs/plans/` — implementation plans and historical execution checklists. New plans should be created with `/ce-plan`.

### Module Structure
```
src/Modules/XFramework.{Module}/
├── {Module}.Api/                    # ASP.NET Web API (endpoints, services, Program.cs)
│   ├── Features/                    # VSA feature folders
│   │   └── {Feature}/{Action}/Endpoint.cs
│   ├── Services/                    # Business logic services
│   ├── GlobalUsings.cs
│   └── Program.cs
├── {Module}.Domain.Shared/          # Shared contracts (requests, responses, enums)
│   ├── Contracts/
│   │   ├── Requests/
│   │   └── Responses/
│   └── GlobalUsings.cs
└── {Module}.Integration/            # Generated service wrappers for cross-module calls
    └── Drivers/
```

### Transport Dual-Path

Every feature endpoint supports two transports:
1. **HTTP** — standard REST via ASP.NET minimal APIs (source-generated from `[MapPost/Get/...]` attributes)
2. **StreamFlow** — binary RPC via SignalR WebSocket (source-generated from `[StreamFlowHandler]` attribute)

Both are generated from the SAME endpoint method via source generators.

---

## Creating a New Feature Endpoint

### Step 1: Define Request & Response in Domain.Shared

**Request** — `{Module}.Domain.Shared/Contracts/Requests/{Name}Request.cs`:
```csharp
using {Module}.Domain.Shared.Contracts.Responses;

namespace {Module}.Domain.Shared.Contracts.Requests;

// For query (returns data):
[MemoryPackable]
public partial record {Name}Request : RequestBase,
    IQuery<QueryResponse<{Name}Response>>,
    IStreamflowRequest<{Name}Request, QueryResponse<{Name}Response>>
{
    // Request properties here
}

// For command (no data return):
[MemoryPackable]
public partial record {Name}Request : RequestBase,
    ICommand<CmdResponse>,
    IStreamflowRequest<{Name}Request, CmdResponse>
{
    // Request properties here
}
```

**Response** — `{Module}.Domain.Shared/Contracts/Responses/{Name}Response.cs`:
```csharp
namespace {Module}.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record {Name}Response
{
    // Response properties here
}
```

Key rules:
- Always use `[MemoryPackable]` + `partial record`
- Implement `IStreamflowRequest<TRequest, TResponse>` for StreamFlow transport
- Implement `IQuery<QueryResponse<T>>` for queries or `ICommand<CmdResponse>` for commands
- Extend `RequestBase` (provides `Metadata` property)

### Step 2: Create the Endpoint

**Location**: `{Module}.Api/Features/{Feature}/{Action}/Endpoint.cs`

```csharp
using {Module}.Domain.Shared.Contracts.Requests;
using {Module}.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;

namespace {Module}.Api.Features.{Feature}.{Action};

public static class {Name}Endpoint
{
    [StreamFlowHandler]
    [MapPost("/api/{feature}/{action}", Tags = ["{Feature}"],
        Summary = "Short description",
        Description = "Detailed description.")]
    public static async Task<Result<{Name}Response>> Handle(
        {Name}Request request,
        I{Service} service,
        CancellationToken ct)
    {
        return await service.DoSomethingAsync(request, ct);
    }
}
```

Key rules:
- Class must be `static`
- Method must be `static`, named `Handle`
- First parameter is the request type
- DI services are injected as subsequent parameters (resolved by source generator)
- `CancellationToken` is always last
- Return `Task<Result<T>>` for queries, `Task<Result>` for commands
- `[StreamFlowHandler]` generates the SignalR handler
- `[MapPost/MapGet/...]` generates the HTTP endpoint
- Both attributes can coexist on the same method

### Available HTTP Attributes
- `[MapPost(route)]` — HTTP POST
- `[MapGet(route)]` — HTTP GET
- `[MapPut(route)]` — HTTP PUT
- `[MapPatch(route)]` — HTTP PATCH
- `[MapDelete(route)]` — HTTP DELETE

Common named parameters: `Tags`, `Summary`, `Description`, `ExcludeFromOpenApi`

### Step 3: Register in Program.cs

Endpoints are auto-registered via `app.MapGeneratedEndpoints()`. No manual registration needed.

```csharp
var builder = XApplication.Configure<Program>();

// Register services
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = (WebApplication)builder.Build();
app.UseCorrelationId();
app.EnsureDatabase<DbContext>();
app.MapGeneratedEndpoints();  // Auto-maps all [MapPost/Get/...] endpoints
app.Run();
```

### What the Source Generators Produce

From a single `[StreamFlowHandler] [MapPost(...)] Handle(...)` method, the generators create:

1. **REST Endpoint** (`*_RestEndpoint.g.cs`) — minimal API with validation, request binding, Result→HTTP mapping
2. **StreamFlow Handler** (`*_StreamFlowHandler.g.cs`) — `ISignalREventHandler` that deserializes from MemoryPack, calls Handle, serializes response
3. **Route Registration** (`GeneratedEndpointRoutes.g.cs`) — `MapGeneratedEndpoints()` extension method
4. **Handler Registration** (`StreamFlowHandlerRegistration.g.cs`) — discovered by `ScanAndRegisterHandlers()`
5. **Service Wrapper** (`{Module}ServiceWrapperGenerator.g.cs`) — `I{Module}ServiceWrapper` with typed methods for cross-module StreamFlow calls

---

## Creating a Service

Services contain business logic, injected into endpoints.

```csharp
public interface IMyService
{
    Task<Result<MyResponse>> DoSomethingAsync(MyRequest request, CancellationToken ct);
}

public class MyService : IMyService
{
    private readonly AppDbContext _db;

    public MyService(AppDbContext db) => _db = db;

    public async Task<Result<MyResponse>> DoSomethingAsync(MyRequest request, CancellationToken ct)
    {
        // Business logic here
        return Result<MyResponse>.Success(response);
        // or: return Result<MyResponse>.Failure("error message", 400);
    }
}
```

Register in Program.cs: `builder.Services.AddScoped<IMyService, MyService>();`

---

## Creating a FluentValidation Validator

Validators are auto-discovered by `AddValidatorsFromAssemblyContaining<Program>()` and auto-run by the generated REST endpoint before calling Handle.

```csharp
using FluentValidation;

namespace {Module}.Api.Features.{Feature}.{Action};

public class {Name}RequestValidator : AbstractValidator<{Name}Request>
{
    public {Name}RequestValidator()
    {
        RuleFor(x => x.PropertyName).NotEmpty();
        RuleFor(x => x.Email).EmailAddress();
    }
}
```

Place the validator next to the endpoint file in the same feature folder.

---

## StreamFlow Transport

### How It Works

```
Client App                    StreamFlow Hub              Service (e.g. IdentityServer)
    |                              |                              |
    |-- Invoke(message) ---------> |                              |
    |                              |-- SendAsync(cmd, msg) -----> |
    |                              |                              | (handler processes)
    |                              | <-- InvokeResponse(resp) --- |
    | <-- return response -------- |                              |
```

- Client calls `hub.Invoke(StreamFlowMessage)` — hub routes to recipient, waits for response
- Recipient processes via generated `ISignalREventHandler`, calls `InvokeResponse` to return
- Hub resolves TCS and returns result to caller — single round-trip

### Calling Another Service via StreamFlow

Use the generated service wrapper:
```csharp
// Inject the wrapper
private readonly IIdentityServerServiceWrapper _identityServer;

// Call it — goes through StreamFlow transport
var result = await _identityServer.AuthenticateIdentity(new AuthenticateIdentityRequest
{
    UserName = "user",
    Password = "pass",
    RoleId = roleId
});
```

Service wrappers are registered via `builder.Services.Add{Module}WrapperServices()` (auto-generated).

### StreamFlow Configuration (appsettings.json)

```json
{
  "StreamFlowConfiguration": {
    "ClientName": "IdentityServer",
    "ServerUrls": ["http://localhost:17000/stream-flow/queue"],
    "MaxParallelInvocationsPerClient": 64,
    "RpcTimeoutSeconds": 30,
    "MaxPendingRpcCalls": 1000,
    "QueueDepth": 10000,
    "MaxRetry": 3,
    "QueueMessages": true,
    "DeadLetterQueueCapacity": 100000
  }
}
```

### MessagePack Protocol

Both server and client MUST use compatible MessagePack configs:
- **Server (Hub)**: `Standard + LZ4BlockArray + UntrustedData`
- **Client (SignalRService)**: `ContractlessStandardResolver + LZ4BlockArray + UntrustedData`

`ContractlessStandardResolver` is a superset of `Standard` — handles `[MessagePackObject]` types identically, plus handles types without attributes.

---

## Creating Integration Tests

### Project Structure
```
src/Tests/{Module}.IntegrationTests/
├── Infrastructure/
│   ├── IntegrationTestFixture.cs    # [SetUpFixture] — starts Postgres, StreamFlow, services
│   └── IntegrationTestBase.cs       # Base class with helpers (CreateDbContext, UniqueUsername, etc.)
├── Tests/
│   └── {Feature}Tests.cs            # [TestFixture] test classes
├── GlobalUsings.cs
└── {Module}.IntegrationTests.csproj
```

### Test Fixture (one-time setup)

The fixture starts a full 3-app architecture using Testcontainers:
1. **PostgreSQL** via Testcontainers
2. **StreamFlow Hub** — routes messages between services
3. **IdentityServer** (or target service) — connects to StreamFlow as a client
4. **Test Client** — connects to StreamFlow, has service wrapper for calling the target

```csharp
[SetUpFixture]
public class IntegrationTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _streamFlowApp = null!;
    private static WebApplication _identityServerApp = null!;
    private static WebApplication _testClientApp = null!;

    public static string ConnectionString { get; private set; } = null!;
    public static IIdentityServerServiceWrapper ServiceWrapper => ...;

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // 1. Start Postgres container
        // 2. Run migrations and seed data
        // 3. Start StreamFlow hub
        // 4. Start IdentityServer (connects to StreamFlow)
        // 5. Seed in-memory tenant cache
        // 6. Start test client (has IIdentityServerServiceWrapper)
        // 7. Wait for StreamFlow clients to connect
        // 8. Register StreamFlow handlers manually (testhost entry assembly workaround)
    }
}
```

**Important**: `ScanAndRegisterHandlers()` only scans the entry assembly. In tests, entry assembly is `testhost`, not the service. Must manually scan:
```csharp
var handlers = typeof(AuthService).Assembly.GetExportedTypes()
    .Where(t => typeof(ISignalREventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .Select(Activator.CreateInstance)
    .Cast<ISignalREventHandler>();
foreach (var handler in handlers)
    handler.Handle(signalRService.Connection!, logger, scopeFactory);
```

### Writing Tests

```csharp
[TestFixture]
public class AuthenticationTests : IntegrationTestBase
{
    // HTTP test — direct HTTP call
    [Test]
    public async Task Http_Authenticate_WithValidCredentials_ReturnsTokenAndSession()
    {
        var username = UniqueUsername();
        var credential = await SeedCredentialWithRole(username, "Password123!");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/authenticate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // StreamFlow test — through service wrapper
    [Test]
    public async Task StreamFlow_Authenticate_WithValidCredentials_ReturnsTokenAndSession()
    {
        var username = UniqueUsername();
        var credential = await SeedCredentialWithRole(username, "Password123!");

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response.Should().NotBeNull();
    }
}
```

### Running Tests

```bash
# Set Testcontainers env vars for remote Docker host
DOCKER_HOST=tcp://100.75.11.49:2375 \
TESTCONTAINERS_HOST_OVERRIDE=100.75.11.49 \
dotnet test src/Tests/IdentityServer.IntegrationTests/ --filter "AuthenticationTests"
```

---

## Creating BenchmarkDotNet Benchmarks

### Project Setup

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="BenchmarkDotNet" />
        <PackageReference Include="Testcontainers.PostgreSql" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\Modules\{Module}\{Module}.Api\{Module}.Api.csproj" />
        <ProjectReference Include="..\..\Modules\{Module}\{Module}.Integration\{Module}.Integration.csproj" />
        <ProjectReference Include="..\..\Modules\XFramework.StreamFlow\StreamFlow.Stream\StreamFlow.Stream.csproj" />
    </ItemGroup>
</Project>
```

### Benchmark Class

Same infrastructure setup as integration tests (Testcontainers + 3-app architecture) in `[GlobalSetup]`. Benchmark methods compare HTTP vs StreamFlow:

```csharp
[Benchmark(Baseline = true)]
public async Task<HttpResponseMessage> Http_HealthCheck()
{
    return await _httpClient.PostAsJsonAsync("/api/health/check", _request);
}

[Benchmark]
public async Task<QueryResponse<HealthCheckResponse>?> StreamFlow_HealthCheck()
{
    return await _serviceWrapper.HealthCheck(request);
}
```

Run: `dotnet run --project src/Tests/{Module}.Benchmarks/ -c Release`

---

## Key Conventions

### Naming
- Endpoints: `{Action}{Entity}Endpoint` (e.g., `AuthenticateEndpoint`, `CreateProductEndpoint`)
- Requests: `{Action}{Entity}Request` (e.g., `AuthenticateIdentityRequest`)
- Responses: `{Action}{Entity}Response` (e.g., `AuthenticateIdentityResponse`)
- Services: `I{Name}Service` / `{Name}Service`
- Feature folders: `Features/{Domain}/{Action}/Endpoint.cs`

### Result Pattern
All endpoints return `Result<T>` or `Result`:
```csharp
Result<T>.Success(data)           // 200 OK with data
Result<T>.Success(data, 201)      // 201 Created with data
Result<T>.Failure("msg", 400)     // 400 Bad Request
Result<T>.Failure("msg", 404)     // 404 Not Found
```

The source-generated REST adapter maps these to HTTP status codes automatically.

### Global Usings

**Api project** (`GlobalUsings.cs`):
```csharp
global using System.Net;
global using Microsoft.EntityFrameworkCore;
global using XFramework.Core.Patterns;
global using XFramework.Domain.Contexts;
global using XFramework.Domain.Shared.BusinessObjects;
global using XFramework.Domain.Shared.Contracts;
global using XFramework.Domain.Shared.Contracts.Requests;
```

**Domain.Shared project** (`GlobalUsings.cs`):
```csharp
global using {Module}.Domain.Shared.Contracts.Responses;
global using MemoryPack;
global using StreamFlow.Domain.Shared.Contracts.Requests;
global using XFramework.Domain.Shared.BusinessObjects;
global using XFramework.Domain.Shared.Contracts.Requests;
global using XFramework.Domain.Shared.Enums;
```

### Docker / Testcontainers

Remote Docker host for tests and deployments:
```bash
export DOCKER_HOST=tcp://100.75.11.49:2375
export TESTCONTAINERS_HOST_OVERRIDE=100.75.11.49
```

### Performance Notes

- StreamFlow achieves **6,441 ops/sec** (8% faster than HTTP) with **56% less memory**
- Uses Invoke/InvokeResponse pattern (not Push) for optimal response routing
- MessagePack (LZ4) for SignalR transport, MemoryPack for payload serialization
- `MaximumParallelInvocationsPerClient = 64` enables concurrent hub processing
- O(1) client lookups via reverse indexes (`ClientKeyByStreamId`, `AbsoluteClientKeyByServiceId`)
