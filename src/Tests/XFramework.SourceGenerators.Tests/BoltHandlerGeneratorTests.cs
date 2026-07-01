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

public sealed record ArchiveThreadRequest : IBoltRequest<ArchiveThreadRequest, CmdResponse>;
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

public sealed record CheckoutSaleRequest : IBoltRequest<CheckoutSaleRequest, CmdResponse<SaleReceipt>>;

public sealed record SaleReceipt;
""";

        var generatedSource = RunGenerator(source, "CheckoutSaleEndpoint_Handle_BoltHandler.g.cs");

        generatedSource.Should().Contain("var sfResponse = new global::XFramework.Domain.Shared.BusinessObjects.CmdResponse<global::Sample.Features.Sales.Checkout.SaleReceipt>();");
        generatedSource.Should().Contain("sfResponse.Response = result.Data;");
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
    public sealed class BoltHandlerAttribute : Attribute;

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
    public sealed class BoltClient
    {
        public void RegisterHandler(
            string requestType,
            Func<ReadOnlyMemory<byte>, Guid, System.Threading.CancellationToken, Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
        {
        }
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
    public class CmdResponse
    {
        public System.Net.HttpStatusCode HttpStatusCode { get; set; }
        public string? Message { get; set; }
    }

    public class CmdResponse<T> : CmdResponse
    {
        public T? Response { get; set; }
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
