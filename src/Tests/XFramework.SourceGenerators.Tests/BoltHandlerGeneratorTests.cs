using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using XFramework.SourceGenerators;

namespace XFramework.SourceGenerators.Tests;

[TestFixture]
public sealed class BoltHandlerGeneratorTests
{
    [Test]
    public void GenerateRestEndpoint_BodylessEndpointWithPositionalRecordRequest_UsesConstructor()
    {
        const string source = """

namespace Sample.Features.Products.Get;

using System;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

public static class GetProductEndpoint
{
    [MapGet("/api/products/{id:guid}")]
    public static Task<Result<ProductResponse>> Handle(
        GetProductByIdRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        return Task.FromResult(Result<ProductResponse>.Success(new ProductResponse()));
    }
}

public record GetProductByIdRequest(Guid Id);

public sealed class ProductResponse;

public sealed class ProductService;
""";

        var generatedSource = RunGenerator(source, "GetProductEndpoint_Handle_RestEndpoint.g.cs");

        generatedSource.Should().Contain(
            "var request = new global::Sample.Features.Products.Get.GetProductByIdRequest(id.GetValueOrDefault());");
        generatedSource.Should().NotContain("request.Id =");
    }

    [Test]
    public void GenerateRestEndpoint_BodylessEndpointWithInitOnlyQueryRequest_UsesObjectInitializer()
    {
        const string source = """

namespace Sample.Features.Products.GetList;

using System;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

public static class GetProductsListEndpoint
{
    [MapGet("/api/products")]
    public static Task<Result<PaginatedProductResponse>> Handle(
        GetProductsRequest request,
        ProductService productService,
        CancellationToken ct)
    {
        return Task.FromResult(Result<PaginatedProductResponse>.Success(new PaginatedProductResponse()));
    }
}

public record GetProductsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsAvailable { get; init; }
}

public sealed class PaginatedProductResponse;

public sealed class ProductService;
""";

        var generatedSource = RunGenerator(source, "GetProductsListEndpoint_Handle_RestEndpoint.g.cs");

        generatedSource.Should().Contain("var request = new global::Sample.Features.Products.GetList.GetProductsRequest()");
        generatedSource.Should().Contain("Page = page.GetValueOrDefault(1),");
        generatedSource.Should().Contain("PageSize = pageSize.GetValueOrDefault(10),");
        generatedSource.Should().Contain("Search = search,");
        generatedSource.Should().NotContain("request.Page =");
        generatedSource.Should().NotContain("request.Search =");
    }

    [Test]
    public void GenerateBoltHandler_BareCommandResponse_DoesNotAssignResponsePayload()
    {
        const string source = """

namespace Sample.Features.Messages.Archive;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class ArchiveThreadEndpoint
{
    [BoltHandler]
    public static Task<Result> Handle(
        ArchiveThreadRequest request,
        CancellationToken ct)
    {
        return Task.FromResult(new Result());
    }
}

public sealed record ArchiveThreadRequest : RequestBase, IBoltRequest<ArchiveThreadRequest, CmdResponse>;
""";

        var generatedSource = RunGenerator(source, "ArchiveThreadEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().Contain("var sfResponse = new global::XFramework.Domain.Shared.BusinessObjects.CmdResponse();");
        generatedSource.Should().NotContain("sfResponse.Response = result.Data;");
    }

    [Test]
    public void GenerateBoltHandler_TypedCommandResponse_AssignsResponsePayload()
    {
        const string source = """

namespace Sample.Features.Sales.Checkout;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class CheckoutSaleEndpoint
{
    [BoltHandler]
    public static Task<Result<SaleReceipt>> Handle(
        CheckoutSaleRequest request,
        CancellationToken ct)
    {
        return Task.FromResult(Result<SaleReceipt>.Success(new SaleReceipt()));
    }
}

public sealed record CheckoutSaleRequest : RequestBase, IBoltRequest<CheckoutSaleRequest, CmdResponse<SaleReceipt>>;

public sealed record SaleReceipt;
""";

        var generatedSource = RunGenerator(source, "CheckoutSaleEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().Contain("var sfResponse = new global::XFramework.Domain.Shared.BusinessObjects.CmdResponse<global::Sample.Features.Sales.Checkout.SaleReceipt>();");
        generatedSource.Should().Contain("sfResponse.Response = result.Data;");
    }

    [Test]
    public void GenerateBoltHandler_WithDetectedValidator_ReturnsBadRequestEnvelopeBeforeHandler()
    {
        const string source = """

namespace Sample.Features.Users.Create;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using FluentValidation;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class CreateUserEndpoint
{
    [MapPost("/api/users")]
    [BoltHandler]
    public static Task<Result<UserResponse>> Handle(
        CreateUserRequest request,
        UserService userService,
        CancellationToken ct)
    {
        return Task.FromResult(Result<UserResponse>.Success(new UserResponse()));
    }
}

public sealed record CreateUserRequest(string Name) : RequestBase, IBoltRequest<CreateUserRequest, QueryResponse<UserResponse>>;

public sealed class UserResponse;

public sealed class UserService;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>;
""";

        var generatedSource = RunGenerator(source, "CreateUserEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().Contain(
            "var validator = scope.ServiceProvider.GetRequiredService<FluentValidation.IValidator<global::Sample.Features.Users.Create.CreateUserRequest>>();");
        generatedSource.Should().Contain("var validationResult = await validator.ValidateAsync(request, ct);");
        generatedSource.Should().Contain(".GroupBy(static e => e.PropertyName)");
        generatedSource.Should().Contain(".ToDictionary(static g => g.Key, static g => g.Select(static e => e.ErrorMessage).ToArray());");
        generatedSource.Should().Contain("validationResponse.HttpStatusCode = System.Net.HttpStatusCode.BadRequest;");
        generatedSource.Should().Contain("validationResponse.Message = \"Validation failed\";");
        generatedSource.Should().Contain("validationResponse.ValidationErrors = errors;");
        generatedSource.Should().Contain("var validationResponseBytes = MemoryPackSerializer.Serialize(validationResponse);");
        generatedSource.Should().Contain(
            "var @userService = scope.ServiceProvider.GetRequiredService<global::Sample.Features.Users.Create.UserService>();");
        generatedSource.IndexOf("var validationResult = await validator.ValidateAsync(request, ct);", StringComparison.Ordinal)
            .Should()
            .BeLessThan(generatedSource.IndexOf("var @userService = scope.ServiceProvider.GetRequiredService", StringComparison.Ordinal));
        generatedSource.IndexOf("var validationResult = await validator.ValidateAsync(request, ct);", StringComparison.Ordinal)
            .Should()
            .BeLessThan(generatedSource.IndexOf("var result = await global::Sample.Features.Users.Create.CreateUserEndpoint.Handle", StringComparison.Ordinal));
    }

    [Test]
    public void GenerateRestEndpoint_WithDetectedValidator_ReturnsValidationProblemBeforeHandler()
    {
        const string source = """

namespace Sample.Features.Users.Create;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using FluentValidation;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class CreateUserEndpoint
{
    [MapPost("/api/users")]
    [BoltHandler]
    public static Task<Result<UserResponse>> Handle(
        CreateUserRequest request,
        UserService userService,
        CancellationToken ct)
    {
        return Task.FromResult(Result<UserResponse>.Success(new UserResponse()));
    }
}

public sealed record CreateUserRequest(string Name) : RequestBase, IBoltRequest<CreateUserRequest, QueryResponse<UserResponse>>;

public sealed class UserResponse;

public sealed class UserService;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>;
""";

        var generatedSource = RunGenerator(source, "CreateUserEndpoint_Handle_RestEndpoint.g.cs");

        generatedSource.Should().Contain(
            "FluentValidation.IValidator<global::Sample.Features.Users.Create.CreateUserRequest> validator");
        generatedSource.Should().Contain("var validationResult = await validator.ValidateAsync(request, ct);");
        generatedSource.Should().Contain(".GroupBy(static e => e.PropertyName)");
        generatedSource.Should().Contain("return TypedResults.ValidationProblem(errors);");
        generatedSource.Should().Contain("Results<Ok<global::Sample.Features.Users.Create.UserResponse>, ValidationProblem, NotFound, ProblemHttpResult>");
        generatedSource.IndexOf("var validationResult = await validator.ValidateAsync(request, ct);", StringComparison.Ordinal)
            .Should()
            .BeLessThan(generatedSource.IndexOf("var result = await global::Sample.Features.Users.Create.CreateUserEndpoint.Handle", StringComparison.Ordinal));
    }

    [Test]
    public void GenerateBoltHandler_WithoutDetectedValidator_DoesNotEmitValidation()
    {
        const string source = """

namespace Sample.Features.Health.Check;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class CheckHealthEndpoint
{
    [BoltHandler]
    public static Task<Result<HealthResponse>> Handle(
        CheckHealthRequest request,
        CancellationToken ct)
    {
        return Task.FromResult(Result<HealthResponse>.Success(new HealthResponse()));
    }
}

public sealed record CheckHealthRequest : RequestBase, IBoltRequest<CheckHealthRequest, QueryResponse<HealthResponse>>;

public sealed class HealthResponse;
""";

        var generatedSource = RunGenerator(source, "CheckHealthEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().NotContain("IValidator<");
        generatedSource.Should().NotContain("ValidateAsync(request, ct)");
        generatedSource.Should().NotContain("validationResponse");
    }

    [Test]
    public void GenerateBoltHandler_DefaultAuthorization_RunsBeforeValidationAndEndpointServices()
    {
        const string source = """

namespace Sample.Features.Users.Create;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using FluentValidation;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class CreateUserEndpoint
{
    [BoltHandler]
    public static Task<Result<UserResponse>> Handle(
        CreateUserRequest request,
        UserService userService,
        CancellationToken ct) =>
        Task.FromResult(Result<UserResponse>.Success(new UserResponse()));
}

public sealed record CreateUserRequest(string Name) : RequestBase,
    IBoltRequest<CreateUserRequest, QueryResponse<UserResponse>>;
public sealed class UserResponse;
public sealed class UserService;
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>;
""";

        var generatedSource = RunGenerator(source, "CreateUserEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().Contain("BoltInboundRequestContext context");
        generatedSource.Should().Contain("GetRequiredService<IBoltServiceInvocationAuthorizer>()");
        generatedSource.Should().Contain("request.Metadata,");
        generatedSource.Should().Contain("requiredScopes: null,");
        generatedSource.Should().Contain("allowedCallers: null,");
        generatedSource.Should().Contain("(System.Net.HttpStatusCode)authorization.StatusCode");
        generatedSource.IndexOf(".AuthorizeAsync(", StringComparison.Ordinal)
            .Should().BeLessThan(generatedSource.IndexOf("ValidateAsync(request, ct)", StringComparison.Ordinal));
        generatedSource.IndexOf(".AuthorizeAsync(", StringComparison.Ordinal)
            .Should().BeLessThan(generatedSource.IndexOf("GetRequiredService<global::Sample.Features.Users.Create.UserService>", StringComparison.Ordinal));
    }

    [Test]
    public void GenerateBoltHandler_ExplicitServicePolicy_EmitsScopesAndAllowedCallers()
    {
        const string source = """

namespace Sample.Features.Security.Restricted;

using System.Threading;
using System.Threading.Tasks;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

public static class RestrictedEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = ["wallets.write", "wallets.approve"],
        AllowedServiceCallers = ["XFramework.Portal"])]
    public static Task<Result> Handle(RestrictedRequest request, CancellationToken ct) =>
        Task.FromResult(new Result());
}

public sealed record RestrictedRequest : RequestBase,
    IBoltRequest<RestrictedRequest, CmdResponse>;
""";

        var generatedSource = RunGenerator(source, "RestrictedEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().Contain(
            "requiredScopes: new string[] { \"wallets.write\", \"wallets.approve\" },");
        generatedSource.Should().Contain(
            "allowedCallers: new string[] { \"XFramework.Portal\" },");
    }

    private static string RunGenerator(string source, string generatedHintName)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "BoltHandlerGeneratorTests",
            [
                CSharpSyntaxTree.ParseText(CommonStubs, parseOptions),
                CSharpSyntaxTree.ParseText(source, parseOptions)
            ],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var inputDiagnostics = compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        inputDiagnostics.Should().BeEmpty();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new BoltHandlerGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        generatorDiagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        outputCompilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        var generatedSource = driver.GetRunResult()
            .GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith(generatedHintName, StringComparison.Ordinal))
            .GetText()
            .ToString();

        generatedSource.Should().NotBeNullOrWhiteSpace();
        return generatedSource;
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(static reference => reference.Display);
    }

    private const string CommonStubs = """
global using System.Threading.Tasks;

using System;
using System.Collections.Generic;

namespace XFramework.Integration.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BoltHandlerAttribute : Attribute
    {
        public string[]? RequiredServiceScopes { get; set; }
        public string[]? AllowedServiceCallers { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MapPostAttribute(string route) : Attribute
    {
        public string Route { get; } = route;
        public string[]? Tags { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public bool ExcludeFromOpenApi { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MapGetAttribute(string route) : Attribute
    {
        public string Route { get; } = route;
        public string[]? Tags { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public bool ExcludeFromOpenApi { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MapDeleteAttribute(string route) : Attribute
    {
        public string Route { get; } = route;
        public string[]? Tags { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public bool ExcludeFromOpenApi { get; set; }
    }
}

namespace XFramework.Integration.Abstractions
{
    public interface IBoltHandler
    {
        void Register(
            Bolt.Client.BoltClient client,
            Microsoft.Extensions.Logging.ILogger logger,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory);
    }
}

namespace Bolt.Client
{
    public readonly record struct BoltInboundRequestContext(Guid RequestId, int SenderHash);

    public sealed class BoltClient
    {
        public void RegisterHandler(
            string requestType,
            Func<ReadOnlyMemory<byte>, Guid, System.Threading.CancellationToken, Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
        {
        }

        public void RegisterHandler(
            string requestType,
            Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, System.Threading.CancellationToken, Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
        {
        }
    }
}

namespace XFramework.Integration.Security
{
    public interface IBoltServiceInvocationAuthorizer
    {
        Task<TrustedServiceInvocationResult> AuthorizeAsync(
            XFramework.Domain.Shared.BusinessObjects.RequestMetadata? metadata,
            Bolt.Client.BoltInboundRequestContext requestContext,
            IReadOnlyCollection<string>? requiredScopes = null,
            IReadOnlyCollection<string>? allowedCallers = null,
            System.Threading.CancellationToken ct = default);
    }

    public sealed class TrustedServiceInvocationResult
    {
        public bool IsSuccess { get; init; }
        public int StatusCode { get; init; }
    }
}

namespace Bolt.Domain.Shared.Contracts.Requests
{
    public interface IBoltRequest<TRequest, TResponse>;
}

namespace MemoryPack
{
    public static class MemoryPackSerializer
    {
        public static T? Deserialize<T>(ReadOnlySpan<byte> span) => default;

        public static byte[] Serialize<T>(T value) => [];
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceScopeFactory
    {
        IServiceScope CreateScope();

        AsyncServiceScope CreateAsyncScope();
    }

    public interface IServiceScope : IDisposable
    {
        IServiceProvider ServiceProvider { get; }
    }

    public readonly struct AsyncServiceScope : IAsyncDisposable
    {
        public IServiceProvider ServiceProvider => default!;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public static class ServiceProviderServiceExtensions
    {
        public static T GetRequiredService<T>(this IServiceProvider provider) => default!;
    }
}

namespace Microsoft.Extensions.Logging
{
    public interface ILogger
    {
        void LogInformation(string message, params object[] args);

        void LogError(Exception exception, string message, params object[] args);
    }
}

namespace XFramework.Core.Patterns
{
    public class Result
    {
        public bool IsSuccess { get; init; } = true;
        public int StatusCode { get; init; } = 200;
        public string? Message { get; init; }
    }

    public sealed class Result<T> : Result
    {
        public T? Data { get; init; }

        public static Result<T> Success(T data)
        {
            return new Result<T> { Data = data };
        }
    }
}

namespace XFramework.Domain.Shared.BusinessObjects
{
    public sealed class RequestMetadata;

    public record RequestBase
    {
        public RequestMetadata Metadata { get; set; } = new();
    }

    public class CmdResponse
    {
        public System.Net.HttpStatusCode HttpStatusCode { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
    }

    public class CmdResponse<T> : CmdResponse
    {
        public T? Response { get; set; }
    }

    public class QueryResponse<T> : CmdResponse<T>;
}

namespace FluentValidation
{
    public interface IValidator<T>
    {
        Task<ValidationResult> ValidateAsync(T instance, System.Threading.CancellationToken cancellation = default);
    }

    public abstract class AbstractValidator<T> : IValidator<T>
    {
        public Task<ValidationResult> ValidateAsync(T instance, System.Threading.CancellationToken cancellation = default)
        {
            return Task.FromResult(new ValidationResult());
        }
    }

    public sealed class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<ValidationFailure> Errors { get; set; } = new();
    }

    public sealed class ValidationFailure
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

namespace Microsoft.AspNetCore.Routing
{
    public interface IEndpointRouteBuilder;
}

namespace Microsoft.AspNetCore.Builder
{
    public static class EndpointRouteBuilderExtensions
    {
        public static RouteHandlerBuilder MapPost(
            this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app,
            string pattern,
            Delegate handler)
        {
            return new RouteHandlerBuilder();
        }

        public static RouteHandlerBuilder MapGet(
            this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app,
            string pattern,
            Delegate handler)
        {
            return new RouteHandlerBuilder();
        }

        public static RouteHandlerBuilder MapDelete(
            this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app,
            string pattern,
            Delegate handler)
        {
            return new RouteHandlerBuilder();
        }
    }

    public sealed class RouteHandlerBuilder
    {
        public RouteHandlerBuilder WithName(string name) => this;
        public RouteHandlerBuilder WithTags(params string[] tags) => this;
        public RouteHandlerBuilder WithSummary(string summary) => this;
        public RouteHandlerBuilder WithDescription(string description) => this;
        public RouteHandlerBuilder ExcludeFromDescription() => this;
    }
}

namespace Microsoft.AspNetCore.Http
{
    public static class TypedResults
    {
        public static Microsoft.AspNetCore.Http.HttpResults.Ok<T> Ok<T>(T value) => new();
        public static Microsoft.AspNetCore.Http.HttpResults.NotFound NotFound() => new();

        public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(
            string? detail = null,
            int? statusCode = null)
        {
            return new Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult();
        }

        public static Microsoft.AspNetCore.Http.HttpResults.ValidationProblem ValidationProblem(
            Dictionary<string, string[]> errors)
        {
            return new Microsoft.AspNetCore.Http.HttpResults.ValidationProblem();
        }
    }
}

namespace Microsoft.AspNetCore.Http.HttpResults
{
    public sealed class Ok<T>;

    public sealed class NotFound;

    public sealed class ProblemHttpResult;

    public sealed class ValidationProblem;

    public sealed class Results<T1, T2, T3>
    {
        public static implicit operator Results<T1, T2, T3>(T1 result) => new();
        public static implicit operator Results<T1, T2, T3>(T2 result) => new();
        public static implicit operator Results<T1, T2, T3>(T3 result) => new();
    }

    public sealed class Results<T1, T2, T3, T4>
    {
        public static implicit operator Results<T1, T2, T3, T4>(T1 result) => new();
        public static implicit operator Results<T1, T2, T3, T4>(T2 result) => new();
        public static implicit operator Results<T1, T2, T3, T4>(T3 result) => new();
        public static implicit operator Results<T1, T2, T3, T4>(T4 result) => new();
    }
}
""";
}
