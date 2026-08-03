using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using XFramework.SourceGenerators;

namespace XFramework.SourceGenerators.Tests;

[TestFixture]
public sealed class DataContextRegistrationGeneratorTests
{
    [Test]
    public void GenerateRegistration_OnlyExplicitOptInIsMutableRegardlessOfEndpointActions()
    {
        const string source = """
namespace Sample;

using XFramework.Domain.Shared.Attributes;

[GenerateEndpoints(Actions = EndpointActions.None)]
public sealed class QueryOnlyEntity;

[GenerateEndpoints(Actions = EndpointActions.All)]
public sealed class BroadEndpointEntity;

[AllowRemoteDataContextMutation]
[GenerateEndpoints(Actions = EndpointActions.None)]
public sealed class AdminMutableEntity;

[GenerateEndpoints(Actions = EndpointActions.Create)]
public sealed class GeneratedCreateEntity;
""";

        var generatedSource = RunGenerator(source);

        generatedSource.Should().Contain("[\"QueryOnlyEntity\"] = typeof(global::Sample.QueryOnlyEntity),");
        generatedSource.Should().Contain("[\"BroadEndpointEntity\"] = typeof(global::Sample.BroadEndpointEntity),");
        generatedSource.Should().Contain("[\"AdminMutableEntity\"] = typeof(global::Sample.AdminMutableEntity),");
        generatedSource.Should().Contain("[\"GeneratedCreateEntity\"] = typeof(global::Sample.GeneratedCreateEntity),");

        generatedSource.Should().Contain("\"AdminMutableEntity\",");
        generatedSource.Should().NotContain("\"BroadEndpointEntity\",");
        generatedSource.Should().NotContain("\"GeneratedCreateEntity\",");
        generatedSource.Should().NotContain("\"QueryOnlyEntity\",");
    }

    private static string RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "SampleModule.Api",
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
            [new DataContextRegistrationGenerator().AsSourceGenerator()],
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
            .Single(tree => tree.FilePath.EndsWith("DataContextEntityRegistrations.g.cs", StringComparison.Ordinal))
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
using System;

namespace XFramework.Domain.Shared.Attributes
{
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
        public EndpointActions Actions { get; set; } = EndpointActions.All;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AllowRemoteDataContextMutationAttribute : Attribute;
}
""";
}
