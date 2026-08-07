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
    public void GenerateRegistration_EmitsOperationSpecificAuthorizationAndServiceOnlyPolicies()
    {
        const string source = """
namespace Sample;

using XFramework.Domain.Shared.Attributes;

[RequireGeneratedActorAttribute("region", "apac", Actions = EndpointActions.Get | EndpointActions.GetList)]
[RequireGeneratedActorAttribute("clearance", "elevated", Actions = EndpointActions.Update)]
[AllowGeneratedServiceAccess("XFramework.Reporting", RequiredScopes = ["reports.read"], Actions = EndpointActions.Get | EndpointActions.GetList)]
[GenerateEndpoints(
    Actions = EndpointActions.All,
    AuthorizationFeature = "wallets.reporting",
    ReadCapability = "view",
    CreateCapability = "create",
    UpdateCapability = "update",
    DeleteCapability = "delete",
    Roles = ["Admin", "Auditor"])]
public sealed class SecuredReport;
""";

        var generatedSource = RunGenerator(source);
        var readPolicy = GetPolicyBlock(generatedSource, "Read");
        var createPolicy = GetPolicyBlock(generatedSource, "Create");
        var updatePolicy = GetPolicyBlock(generatedSource, "Update");
        var deletePolicy = GetPolicyBlock(generatedSource, "Delete");

        readPolicy.Should().Contain("AuthorizationFeature = \"wallets.reporting\"");
        readPolicy.Should().Contain("RequiredCapability = \"wallets.reporting:view\"");
        createPolicy.Should().Contain("RequiredCapability = \"wallets.reporting:create\"");
        updatePolicy.Should().Contain("RequiredCapability = \"wallets.reporting:update\"");
        deletePolicy.Should().Contain("RequiredCapability = \"wallets.reporting:delete\"");
        generatedSource.Should().Contain("RequiredRoles = [\"Admin\", \"Auditor\"]", Exactly.Times(4));

        readPolicy.Should().Contain("[\"region\"] = \"apac\"");
        readPolicy.Should().NotContain("clearance");
        createPolicy.Should().NotContain("region");
        createPolicy.Should().NotContain("clearance");
        updatePolicy.Should().Contain("[\"clearance\"] = \"elevated\"");
        updatePolicy.Should().NotContain("region");
        deletePolicy.Should().NotContain("region");
        deletePolicy.Should().NotContain("clearance");

        readPolicy.Should().Contain("AllowServiceOnly = true");
        readPolicy.Should().Contain("AllowedServiceCallers = [\"XFramework.Reporting\"]");
        readPolicy.Should().Contain("RequiredServiceScopes = [\"reports.read\"]");
        createPolicy.Should().Contain("AllowServiceOnly = false");
        updatePolicy.Should().Contain("AllowServiceOnly = false");
        deletePolicy.Should().Contain("AllowServiceOnly = false");
    }

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

        var mutableSection = generatedSource[
            generatedSource.IndexOf("public static HashSet<string> GetDataContextMutableEntityTypes", StringComparison.Ordinal)..
            generatedSource.IndexOf("public static Dictionary<string, string> GetDataContextServiceWrapperMap", StringComparison.Ordinal)];
        mutableSection.Should().Contain("\"AdminMutableEntity\",");
        mutableSection.Should().NotContain("\"BroadEndpointEntity\",");
        mutableSection.Should().NotContain("\"GeneratedCreateEntity\",");
        mutableSection.Should().NotContain("\"QueryOnlyEntity\",");
    }

    [Test]
    public void GenerateRegistration_RemoteMutationOptInEmitsProtectedMutationPolicies()
    {
        const string source = """
        namespace Sample;

        using XFramework.Domain.Shared.Attributes;

        [AllowRemoteDataContextMutation]
        [GenerateEndpoints(
            Actions = EndpointActions.Get | EndpointActions.GetList,
            AuthorizationFeature = "identity")]
        public sealed class RemoteMutableEntity;
        """;

        var generatedSource = RunGenerator(source);

        GetPolicyBlock(generatedSource, "Read").Should().Contain("AllowRemoteQuery = true");
        GetPolicyBlock(generatedSource, "Create").Should().Contain("AllowRemoteMutation = true");
        GetPolicyBlock(generatedSource, "Update").Should().Contain("AllowRemoteMutation = true");
        GetPolicyBlock(generatedSource, "Delete").Should().Contain("AllowRemoteMutation = true");
    }

    [Test]
    public void GenerateRegistration_RestOnlyEntityIsNotExposedToRemoteQueries()
    {
        const string source = """
        namespace Sample;
        using XFramework.Domain.Shared.Attributes;

        [GenerateEndpoints(Type = EndpointType.Rest, AuthorizationFeature = "identity.contacts")]
        public sealed class RestOnlyEntity;
        """;

        GetPolicyBlock(RunGenerator(source), "Read").Should().Contain("AllowRemoteQuery = false");
    }

    [TestCase("wallets reporting", "view")]
    [TestCase("wallets.reporting", "View")]
    [TestCase("wallets.reporting", "view:all")]
    [TestCase("wallets.reporting", "inspect")]
    public void GenerateRegistration_MalformedAuthorizationTaxonomyReportsDiagnostic(
        string feature,
        string capability)
    {
        var source = $$"""
        namespace Sample;
        using XFramework.Domain.Shared.Attributes;

        [GenerateEndpoints(AuthorizationFeature = "{{feature}}", ReadCapability = "{{capability}}")]
        public sealed class InvalidTaxonomyEntity;
        """;

        RunGeneratorWithDiagnostics(source).Diagnostics
            .Should().Contain(diagnostic => diagnostic.Id == "XFWGEN002");
    }

    [TestCase("identity.tenants")]
    [TestCase("identity.tenants:inspect")]
    [TestCase("Identity.tenants:manage")]
    public void GenerateRegistration_MalformedCrossTenantCapabilityReportsDiagnostic(
        string capability)
    {
        var source = $$"""
        namespace Sample;
        using XFramework.Domain.Shared.Attributes;

        [GenerateEndpoints(
            AuthorizationFeature = "wallets",
            CrossTenantCapability = "{{capability}}")]
        public sealed class InvalidCrossTenantCapabilityEntity;
        """;

        RunGeneratorWithDiagnostics(source).Diagnostics
            .Should().Contain(diagnostic => diagnostic.Id == "XFWGEN002");
    }

    [Test]
    public void GenerateRegistration_ActorNoneWithGeneratedCapabilityReportsDiagnostic()
    {
        const string source = """
        namespace Sample;
        using XFramework.Domain.Shared.Attributes;

        [GenerateEndpoints(
            ActorRequirement = GeneratedActorRequirement.None,
            AuthorizationFeature = "wallets.reporting")]
        public sealed class ContradictoryEntity;
        """;

        RunGeneratorWithDiagnostics(source).Diagnostics
            .Should().Contain(diagnostic => diagnostic.Id == "XFWGEN002");
    }

    [Test]
    public void GenerateRegistration_SecuredEntityWithoutFeature_ReportsCompatibilityWarning()
    {
        const string source = """
        namespace Sample;
        using XFramework.Domain.Shared.Attributes;

        [GenerateEndpoints(Actions = EndpointActions.Get)]
        public sealed class LegacySecuredEntity;
        """;

        var result = RunGeneratorWithDiagnostics(source);

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "XFWGEN001");
        result.Source.Should().Contain("EntityTypeName = \"LegacySecuredEntity\"");
    }

    private static string RunGenerator(string source) => RunGeneratorWithDiagnostics(source).Source;

    private static (string Source, IReadOnlyList<Diagnostic> Diagnostics) RunGeneratorWithDiagnostics(string source)
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

        var runResult = driver.GetRunResult();
        var generatedSource = runResult
            .GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("DataContextEntityRegistrations.g.cs", StringComparison.Ordinal))
            .GetText()
            .ToString();

        generatedSource.Should().NotBeNullOrWhiteSpace();
        return (generatedSource, runResult.Diagnostics);
    }

    private static string GetPolicyBlock(string generatedSource, string operation)
    {
        var operationMarker = $"Operation = GeneratedEntityOperation.{operation}";
        var operationIndex = generatedSource.IndexOf(operationMarker, StringComparison.Ordinal);
        operationIndex.Should().BeGreaterThanOrEqualTo(0);

        var blockStart = generatedSource.LastIndexOf(
            "new GeneratedEntityAuthorizationPolicy",
            operationIndex,
            StringComparison.Ordinal);
        var blockEnd = generatedSource.IndexOf("        },", operationIndex, StringComparison.Ordinal);
        blockStart.Should().BeGreaterThanOrEqualTo(0);
        blockEnd.Should().BeGreaterThan(operationIndex);
        return generatedSource[blockStart..blockEnd];
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
        public EndpointType Type { get; set; } = EndpointType.Both;
        public bool RequireAuthorization { get; set; } = true;
        public string[]? Roles { get; set; }
        public GeneratedActorRequirement ActorRequirement { get; set; } = GeneratedActorRequirement.Required;
        public GeneratedTenantAccessMode TenantAccessMode { get; set; } = GeneratedTenantAccessMode.ActorTenant;
        public string CrossTenantCapability { get; set; } = "identity.tenants:manage";
        public string? AuthorizationFeature { get; set; }
        public string ReadCapability { get; set; } = "view";
        public string CreateCapability { get; set; } = "create";
        public string UpdateCapability { get; set; } = "update";
        public string DeleteCapability { get; set; } = "delete";
    }

    public enum GeneratedActorRequirement
    {
        Required,
        Optional,
        None
    }

    public enum GeneratedTenantAccessMode { ActorTenant, DelegatedTenant, Tenantless }

    public enum EndpointType { Service = 1, Rest = 2, Both = 3 }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class RequireGeneratedActorAttributeAttribute(string name, string value) : Attribute
    {
        public string Name { get; } = name;
        public string Value { get; } = value;
        public EndpointActions Actions { get; set; } = EndpointActions.All;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AllowGeneratedServiceAccessAttribute(params string[] allowedCallers) : Attribute
    {
        public string[] AllowedCallers { get; } = allowedCallers;
        public string[] RequiredScopes { get; set; } = [];
        public EndpointActions Actions { get; set; } = EndpointActions.Get | EndpointActions.GetList;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AllowRemoteDataContextMutationAttribute : Attribute;
}

namespace XFramework.Integration.Security
{
    public enum ActorRequirement
    {
        Required,
        Optional,
        None
    }

    public enum TenantAccessMode { ActorTenant, DelegatedTenant, Tenantless }
}

namespace XFramework.Core.DataContext
{
    public enum GeneratedEntityOperation
    {
        Read,
        Create,
        Update,
        Delete
    }

    public sealed class GeneratedEntityAuthorizationPolicy
    {
        public required string EntityTypeName { get; init; }
        public required GeneratedEntityOperation Operation { get; init; }
        public bool RequireAuthorization { get; init; }
        public XFramework.Integration.Security.ActorRequirement ActorRequirement { get; init; }
        public XFramework.Integration.Security.TenantAccessMode TenantAccessMode { get; init; }
        public string? AuthorizationFeature { get; init; }
        public string? RequiredCapability { get; init; }
        public System.Collections.Generic.IReadOnlyCollection<string> RequiredCrossTenantActorCapabilities { get; init; } = [];
        public System.Collections.Generic.IReadOnlyCollection<string> RequiredRoles { get; init; } = [];
        public System.Collections.Generic.IReadOnlyDictionary<string, string> RequiredActorAttributes { get; init; } =
            new System.Collections.Generic.Dictionary<string, string>();
        public bool AllowRemoteQuery { get; init; }
        public bool AllowRemoteMutation { get; init; }
        public bool AllowServiceOnly { get; init; }
        public System.Collections.Generic.IReadOnlyCollection<string> AllowedServiceCallers { get; init; } = [];
        public System.Collections.Generic.IReadOnlyCollection<string> RequiredServiceScopes { get; init; } = [];
    }
}
""";
}
