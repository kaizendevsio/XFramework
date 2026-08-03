using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using XFramework.SourceGenerators;

namespace XFramework.SourceGenerators.Tests;

[TestFixture]
public sealed class ChangeTrackerGeneratorTests
{
    [Test]
    public void GenerateTracker_ExcludesServerOwnedFieldsFromPatches()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("ExpectedConcurrencyStamp = original.ConcurrencyStamp");
        generatedSource.Should().Contain("changes[\"Name\"]");
        foreach (var property in new[]
                 {
                     "TenantId", "CreatedAt", "ModifiedAt", "IsDeleted", "DeletedAt", "IsEnabled",
                     "ConcurrencyStamp"
                 })
        {
            generatedSource.Should().NotContain($"changes[\"{property}\"]");
        }
    }

    private static string RunGenerator()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(static reference => reference.Display)
            .ToArray();
        var domainCompilation = CSharpCompilation.Create(
            "Sample.Domain.Shared",
            [CSharpSyntaxTree.ParseText(DomainSource, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var domainAssembly = new MemoryStream();
        domainCompilation.Emit(domainAssembly).Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        var integrationCompilation = CSharpCompilation.Create(
            "Sample.Integration",
            [CSharpSyntaxTree.ParseText("namespace Sample.Integration; public sealed class Marker;", parseOptions)],
            references.Append(MetadataReference.CreateFromImage(domainAssembly.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ChangeTrackerGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGenerators(integrationCompilation);

        driver.GetRunResult().Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
        return driver.GetRunResult().GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("WidgetChangeTracker.g.cs", StringComparison.Ordinal))
            .GetText()
            .ToString();
    }

    private const string DomainSource = """
using System;
using XFramework.Domain.Shared.Attributes;

namespace XFramework.Domain.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GenerateEndpointsAttribute : Attribute;
}

namespace Sample.Domain.Shared
{
    public abstract class BaseModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsEnabled { get; set; }
    }

    [GenerateEndpoints]
    public sealed class Widget : BaseModel
    {
        public string Name { get; set; } = string.Empty;
    }
}
""";
}
