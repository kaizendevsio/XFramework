using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using XFramework.SourceGenerators;

namespace XFramework.SourceGenerators.Tests;

[TestFixture]
public sealed class EntityEndpointGeneratorTests
{
    [Test]
    public void GenerateEndpoints_WithConcreteValidators_ValidatesCreateAndUpdateBeforeServiceCalls()
    {
        var generatedSource = RunGenerator(includeValidators: true);

        generatedSource.Should().Contain(
            "global::FluentValidation.IValidator<global::Sample.CreateWidgetRequest> validator");
        generatedSource.Should().Contain(
            "global::FluentValidation.IValidator<global::Sample.UpdateWidgetRequest> validator");
        generatedSource.Should().Contain("var validationResult = await validator.ValidateAsync(request, ct);");
        generatedSource.Should().Contain("return Results.ValidationProblem(errors);");
        generatedSource.Should().Contain(".ProducesValidationProblem()");
        generatedSource.IndexOf("validator.ValidateAsync(request, ct)", StringComparison.Ordinal)
            .Should().BeLessThan(generatedSource.IndexOf("service.CreateAsync(request, ct)", StringComparison.Ordinal));
        generatedSource.LastIndexOf("validator.ValidateAsync(request, ct)", StringComparison.Ordinal)
            .Should().BeLessThan(generatedSource.IndexOf(
                "service.UpdateAsync(id, expectedConcurrencyStamp, request, ct)",
                StringComparison.Ordinal));
    }

    [Test]
    public void GenerateEndpoints_WithoutValidators_DoesNotInventValidatorDependencies()
    {
        var generatedSource = RunGenerator(includeValidators: false);

        generatedSource.Should().NotContain("FluentValidation.IValidator<");
        generatedSource.Should().NotContain("validator.ValidateAsync(request, ct)");
        generatedSource.Should().Contain("service.CreateAsync(request, ct)");
        generatedSource.Should().Contain("service.UpdateAsync(id, expectedConcurrencyStamp, request, ct)");
    }

    [Test]
    public void GenerateEndpoints_MutationsDeclareRequiredTenantCapabilities()
    {
        var generatedSource = RunGenerator(includeValidators: false);

        generatedSource.Should().Contain("TenantCapabilityRequirement(\"create\")", Exactly.Once());
        generatedSource.Should().Contain("TenantCapabilityRequirement(\"update\")", Exactly.Once());
        generatedSource.Should().Contain("TenantCapabilityRequirement(\"delete\")", Exactly.Once());
        generatedSource.Should().Contain(".RequireAuthorization()", Exactly.Times(5));
        generatedSource.Should().NotContain("TenantCapabilityRequirement(\"view\")");
    }

    [Test]
    public void GenerateEndpoints_BaseModelMutationsRequireConcurrencyStampAndMapConflicts()
    {
        var generatedSource = RunGenerator(includeValidators: false);

        generatedSource.Should().Contain("Guid expectedConcurrencyStamp", Exactly.Times(2));
        generatedSource.Should().Contain(
            "service.UpdateAsync(id, expectedConcurrencyStamp, request, ct)");
        generatedSource.Should().Contain(
            "service.DeleteAsync(id, expectedConcurrencyStamp, ct)");
        generatedSource.Should().Contain("409 => Results.Conflict(result.Message)", Exactly.Times(3));
        generatedSource.Should().Contain(".Produces(StatusCodes.Status409Conflict)", Exactly.Times(3));
        generatedSource.Should().Contain("401 => Results.Unauthorized()", Exactly.Times(5));
        generatedSource.Should().Contain(".Produces(StatusCodes.Status401Unauthorized)", Exactly.Times(5));
        generatedSource.Should().Contain("400 => Results.BadRequest(result.Message)", Exactly.Times(4));
    }

    [Test]
    public void GenerateEndpoints_DoNotAdvertiseInactiveOutputCaching()
    {
        var generatedSource = RunGenerator(includeValidators: false);

        generatedSource.Should().NotContain("TODO: Apply OutputCaching");
        generatedSource.Should().NotContain("-cache");
    }

    [Test]
    public void GenerateEndpoints_DeclareSafeResponseContractsAndRemainInOpenApi()
    {
        var generatedSource = RunGenerator(includeValidators: false);

        generatedSource.Should().Contain(".Produces<GeneratedWidgetResponse>(StatusCodes.Status200OK)", Exactly.Times(2));
        generatedSource.Should().Contain(".Produces<List<GeneratedWidgetResponse>>(StatusCodes.Status200OK)", Exactly.Once());
        generatedSource.Should().Contain(".Produces<GeneratedWidgetResponse>(StatusCodes.Status201Created)", Exactly.Once());
        generatedSource.Should().Contain("GeneratedWidgetResponse.FromEntity(result.Data)", Exactly.Once());
        generatedSource.Should().Contain("GeneratedWidgetResponse.FromEntity(result.Data!)", Exactly.Once());
        generatedSource.Should().NotContain(".Produces<Widget>(");
        generatedSource.Should().NotContain(".Produces<List<Widget>>(");
        generatedSource.Should().NotContain(".ExcludeFromDescription()");
    }

    [Test]
    public void GenerateEndpoints_EmittedSourceHasNoSyntaxErrors()
    {
        var generatedSource = RunGenerator(includeValidators: true);

        CSharpSyntaxTree.ParseText(generatedSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))
            .GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();
    }

    [Test]
    public void GenerateEndpoints_EmitsAggregateRouteRegistration()
    {
        var generatedSource = RunGeneratorTree(
            includeValidators: false,
            "GeneratedEntityEndpointRoutes.g.cs");

        generatedSource.Should().Contain(
            "public static IEndpointRouteBuilder MapGeneratedEntityEndpoints(this IEndpointRouteBuilder app)");
        generatedSource.Should().Contain(
            "global::Sample.WidgetEndpoints.MapWidgetEndpoints(app);");
    }

    private static string RunGenerator(bool includeValidators)
        => RunGeneratorTree(includeValidators, "WidgetEndpoints.g.cs");

    private static string RunGeneratorTree(bool includeValidators, string generatedFileName)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var source = includeValidators ? Source + ValidatorSource : Source;
        var compilation = CSharpCompilation.Create(
            "Sample.Api",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new EntityEndpointGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGenerators(compilation);

        driver.GetRunResult().Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        return driver.GetRunResult()
            .GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith(generatedFileName, StringComparison.Ordinal))
            .GetText()
            .ToString();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(static reference => reference.Display);

    private const string Source = """
using System;
using XFramework.Core.Attributes;

namespace XFramework.Core.Attributes
{
    public enum EndpointType
    {
        Rest = 2,
        Both = 3
    }

    [Flags]
    public enum EndpointActions
    {
        None = 0,
        Create = 1,
        Get = 2,
        GetList = 4,
        Update = 8,
        Delete = 16,
        All = Create | Get | GetList | Update | Delete
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateEndpointsAttribute : Attribute
    {
        public EndpointType Type { get; set; } = EndpointType.Both;
        public EndpointActions Actions { get; set; } = EndpointActions.All;
        public string? RoutePrefix { get; set; }
        public bool RequireAuthorization { get; set; } = true;
        public int CacheDurationSeconds { get; set; } = 300;
        public string[]? Roles { get; set; }
    }
}

namespace XFramework.Domain.Shared.Contracts.Base
{
    public abstract class BaseModel;
}

namespace FluentValidation
{
    public interface IValidator<T>;
    public abstract class AbstractValidator<T> : IValidator<T>;
}

namespace Sample
{
    [GenerateEndpoints(Type = EndpointType.Both, Actions = EndpointActions.All)]
    public sealed class Widget : XFramework.Domain.Shared.Contracts.Base.BaseModel;

    public sealed class CreateWidgetRequest;
    public sealed class UpdateWidgetRequest;
}
""";

    private const string ValidatorSource = """

namespace Sample
{
    public sealed class CreateWidgetRequestValidator :
        FluentValidation.AbstractValidator<CreateWidgetRequest>;

    public sealed class UpdateWidgetRequestValidator :
        FluentValidation.AbstractValidator<UpdateWidgetRequest>;
}
""";
}
