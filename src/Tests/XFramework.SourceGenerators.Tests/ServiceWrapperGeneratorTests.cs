using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using XFramework.SourceGenerators;

namespace XFramework.SourceGenerators.Tests;

[TestFixture]
public sealed class ServiceWrapperGeneratorTests
{
    [Test]
    public void GenerateWrapper_CustomExternalServiceName_UsesConfiguredNameTargetAndDiscoveryPrefix()
    {
        var domainReference = CreateReference(
            "Juan.Barangay.Domain.Shared",
            """
            namespace Juan.Barangay.Domain.Shared.Entities
            {
                using XFramework.Domain.Shared.Attributes;
                using XFramework.Domain.Shared.Contracts.Base;

                [GenerateEndpoints(Actions = EndpointActions.None, RequireAuthorization = true)]
                public sealed class Resident : BaseModel;
            }

            namespace Juan.Barangay.Domain.Shared.Contracts.Residents
            {
                using Bolt.Domain.Shared.Contracts.Requests;
                using XFramework.Domain.Shared.BusinessObjects;
                using XFramework.Domain.Shared.Contracts.Requests;

                public sealed record ResidentResponse;

                public partial record ValidateBarangayIdRequest : RequestBase,
                    IQuery<QueryResponse<ResidentResponse>>,
                    IBoltRequest<ValidateBarangayIdRequest, QueryResponse<ResidentResponse>>;
            }
            """);

        var generatedSource = RunGenerator(
            "Juan.Barangay.Integration",
            domainReference,
            new Dictionary<string, string>
            {
                ["build_property.XFrameworkServiceWrapperName"] = "JuanBarangay",
                ["build_property.XFrameworkServiceWrapperTargetClientName"] = "JuanBarangay",
                ["build_property.XFrameworkServiceWrapperDiscoveryPrefixes"] = "Juan.Barangay"
            });

        generatedSource.Should().Contain("namespace JuanBarangay.Integration.Drivers");
        generatedSource.Should().Contain("#nullable enable");
        generatedSource.Should().Contain("public partial interface IJuanBarangayServiceWrapper");
        generatedSource.Should().Contain("public partial record JuanBarangayServiceWrapper(");
        generatedSource.Should().Contain($"TargetClient = \"{"XFramework.JuanBarangay".ToSha256()}\"");
        generatedSource.Should().Contain("IServiceTokenProvider serviceTokenProvider");
        generatedSource.Should().Contain("IActorAccessTokenProvider actorAccessTokenProvider");
        generatedSource.Should().Contain("BoltInvocationEnvelopeFactory.CreateAsync(");
        generatedSource.Should().Contain("entityForLog is XFramework.Domain.Shared.Contracts.Base.IHasTenantId tenantOwned");
        generatedSource.Should().Contain("Metadata = new RequestMetadata { RequestedTenantId = requestedTenantId }");
        generatedSource.Should().Contain("public IResidentCrudService Resident { get; init; }");
        generatedSource.Should().Contain("services.AddScoped<IJuanBarangayServiceWrapper, JuanBarangayServiceWrapper>();");
        generatedSource.Should().Contain("services.AddScoped<IResidentCrudService>");
        generatedSource.Should().NotContain("services.AddSingleton<IJuanBarangayServiceWrapper");
        generatedSource.Should().NotContain("GetProperty(\"TenantId\")");
        generatedSource.Should().Contain("Task<QueryResponse<Juan.Barangay.Domain.Shared.Contracts.Residents.ResidentResponse>> ValidateBarangayId(Juan.Barangay.Domain.Shared.Contracts.Residents.ValidateBarangayIdRequest request, System.Threading.CancellationToken ct = default);");
        generatedSource.Should().Contain("SendAsync<Juan.Barangay.Domain.Shared.Contracts.Residents.ValidateBarangayIdRequest, Juan.Barangay.Domain.Shared.Contracts.Residents.ResidentResponse>(request, ct);");
        generatedSource.Should().NotContain("namespace Juan.Integration.Drivers");
    }

    [Test]
    public void GenerateWrapper_ConventionModuleName_UsesCanonicalXFrameworkTarget()
    {
        var domainReference = CreateReference(
            "Inventario.Domain.Shared",
            """
            namespace XFramework.Inventario.Domain.Shared.Contracts
            {
                using XFramework.Domain.Shared.Attributes;
                using XFramework.Domain.Shared.Contracts.Base;

                [GenerateEndpoints(Actions = EndpointActions.None)]
                public sealed class Product : BaseModel;
            }

            namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations
            {
                using Bolt.Domain.Shared.Contracts.Requests;
                using XFramework.Domain.Shared.BusinessObjects;
                using XFramework.Domain.Shared.Contracts.Requests;

                public partial record ReserveInventoryRequest : RequestBase,
                    ICommand<CmdResponse>,
                    IBoltRequest<ReserveInventoryRequest, CmdResponse>;
            }
            """);

        var generatedSource = RunGenerator(
            "Inventario.Integration",
            domainReference,
            new Dictionary<string, string>());

        generatedSource.Should().Contain("namespace Inventario.Integration.Drivers");
        generatedSource.Should().Contain("public partial interface IInventarioServiceWrapper");
        generatedSource.Should().Contain($"TargetClient = \"{"XFramework.Inventario".ToSha256()}\"");
        generatedSource.Should().Contain("IServiceTokenProvider serviceTokenProvider");
        generatedSource.Should().Contain("IActorAccessTokenProvider actorAccessTokenProvider");
        generatedSource.Should().Contain("BoltInvocationEnvelopeFactory.CreateAsync(");
        generatedSource.Should().Contain("if (stream.CloseStatus is { } closeStatus");
        generatedSource.Should().Contain("DataContext streaming request failed with status");
        generatedSource.Should().Contain("public IProductCrudService Product { get; init; }");
        generatedSource.Should().Contain("Task<CmdResponse> ReserveInventory(XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations.ReserveInventoryRequest request, System.Threading.CancellationToken ct = default);");
        generatedSource.Should().Contain("SendVoidAsync(request, ct);");
    }

    [Test]
    public void GenerateWrapper_PropagatesActorTokenWithoutClientOwnedEntityPolicyOrScopes()
    {
        var domainReference = CreateReference(
            "Inventario.Domain.Shared",
            """
            namespace XFramework.Inventario.Domain.Shared.Contracts
            {
                using XFramework.Domain.Shared.Attributes;
                using XFramework.Domain.Shared.Contracts.Base;

                [GenerateEndpoints(
                    Actions = EndpointActions.All,
                    AuthorizationFeature = "inventario.products",
                    ReadCapability = "inspect",
                    CreateCapability = "add")]
                public sealed class Product : BaseModel;
            }
            """);

        var generatedSource = RunGenerator(
            "Inventario.Integration",
            domainReference,
            new Dictionary<string, string>());

        generatedSource.Should().Contain("IActorAccessTokenProvider actorAccessTokenProvider");
        generatedSource.Should().Contain("_actorAccessTokenProvider = actorAccessTokenProvider;");
        generatedSource.Should().Contain(
            "BoltInvocationEnvelopeFactory.CreateAsync(",
            Exactly.Times(6));
        generatedSource.Should().Contain("XFrameworkServiceScopes.DataContextQuery");
        generatedSource.Should().Contain("XFrameworkServiceScopes.DataContextMutate");
        generatedSource.Should().Contain("XFrameworkServiceScopes.TenantTarget");
        generatedSource.Should().Contain("MemoryPack.MemoryPackSerializer.Deserialize<DataContextResult>(data.Span)");
        generatedSource.Should().Contain("HttpStatusCode = status");
        generatedSource.Should().Contain("Message = failure?.Message");

        generatedSource.Should().NotContain("inventario.products");
        generatedSource.Should().NotContain("RequiredActorRoles");
        generatedSource.Should().NotContain("RequiredActorCapabilities");
        generatedSource.Should().NotContain("RequiredActorAttributes");
        generatedSource.Should().NotContain("AuthorizationFeature");
        generatedSource.Should().NotContain("inventario.products:inspect");
        generatedSource.Should().NotContain("inventario.products:add");
    }

    [Test]
    public void GenerateWrapper_RestOnlyEntityDoesNotGetBoltCrudWrapper()
    {
        var domainReference = CreateReference(
            "Inventario.Domain.Shared",
            """
            namespace XFramework.Inventario.Domain.Shared.Contracts
            {
                using XFramework.Domain.Shared.Attributes;
                using XFramework.Domain.Shared.Contracts.Base;

                [GenerateEndpoints(Type = EndpointType.Rest)]
                public sealed class RestOnlyProduct : BaseModel;

                [GenerateEndpoints(Type = EndpointType.Both)]
                public sealed class BoltProduct : BaseModel;
            }
            """);

        var generatedSource = RunGenerator(
            "Inventario.Integration",
            domainReference,
            new Dictionary<string, string>());

        generatedSource.Should().Contain("public IBoltProductCrudService BoltProduct { get; init; }");
        generatedSource.Should().NotContain("RestOnlyProductCrudService");
    }

    [Test]
    public void GenerateWrapper_TypedCommandResponse_PreservesTypedPayload()
    {
        var domainReference = CreateReference(
            "POS.Domain.Shared",
            """
            namespace POS.Domain.Shared.Contracts.Responses
            {
                public sealed record PosSaleReceiptResponse;
            }

            namespace POS.Domain.Shared.Contracts.Requests
            {
                using Bolt.Domain.Shared.Contracts.Requests;
                using POS.Domain.Shared.Contracts.Responses;
                using XFramework.Domain.Shared.BusinessObjects;
                using XFramework.Domain.Shared.Contracts.Requests;

                public partial record CheckoutPosSaleRequest : RequestBase,
                    ICommand<CmdResponse<PosSaleReceiptResponse>>,
                    IBoltRequest<CheckoutPosSaleRequest, CmdResponse<PosSaleReceiptResponse>>;
            }
            """);

        var generatedSource = RunGenerator(
            "POS.Integration",
            domainReference,
            new Dictionary<string, string>());

        generatedSource.Should().Contain("public partial interface IPOSServiceWrapper");
        generatedSource.Should().Contain("Task<CmdResponse<POS.Domain.Shared.Contracts.Responses.PosSaleReceiptResponse>> CheckoutPosSale(POS.Domain.Shared.Contracts.Requests.CheckoutPosSaleRequest request, System.Threading.CancellationToken ct = default);");
        generatedSource.Should().Contain("SendVoidAsync<POS.Domain.Shared.Contracts.Requests.CheckoutPosSaleRequest, POS.Domain.Shared.Contracts.Responses.PosSaleReceiptResponse>(request, ct);");
    }

    [Test]
    public void GenerateWrapper_ProjectWithManualWrapperDeclaration_SkipsGeneratedWrapper()
    {
        var domainReference = CreateReference(
            "Communications.Domain.Shared",
            """
            namespace Communications.Domain.Shared.Contracts.Requests.Create
            {
                using Bolt.Domain.Shared.Contracts.Requests;
                using XFramework.Domain.Shared.BusinessObjects;
                using XFramework.Domain.Shared.Contracts.Requests;

                public partial record CreateDirectMessageRequest : RequestBase,
                    ICommand<CmdResponse>,
                    IBoltRequest<CreateDirectMessageRequest, CmdResponse>;
            }
            """);

        var generatedSources = RunGeneratorSources(
            "Communications.Integration",
            domainReference,
            new Dictionary<string, string>(),
            """
            namespace Communications.Integration.Drivers
            {
                public interface ICommunicationsServiceWrapper { }
                public sealed record CommunicationsServiceWrapper { }
                public static class CommunicationsServiceWrapperExtensions { }
            }
            """);

        generatedSources.Should().BeEmpty();
    }

    [Test]
    public void GenerateWrapper_ProjectWithPartialWrapperExtensions_StillGeneratesWrapper()
    {
        var domainReference = CreateReference(
            "IdentityServer.Domain.Shared",
            """
            namespace IdentityServer.Domain.Shared.Contracts.Requests
            {
                using Bolt.Domain.Shared.Contracts.Requests;
                using XFramework.Domain.Shared.BusinessObjects;
                using XFramework.Domain.Shared.Contracts.Requests;

                public partial record AuthenticateRequest : RequestBase,
                    IQuery<QueryResponse<string>>,
                    IBoltRequest<AuthenticateRequest, QueryResponse<string>>;
            }
            """);

        var generatedSources = RunGeneratorSources(
            "IdentityServer.Integration",
            domainReference,
            new Dictionary<string, string>(),
            """
            namespace IdentityServer.Integration.Drivers
            {
                public partial interface IIdentityServerServiceWrapper { }
                public partial record IdentityServerServiceWrapper { }
            }
            """);

        generatedSources.Should().ContainSingle();
        generatedSources.Single().Should().Contain("Authenticate(");
    }

    private static string RunGenerator(
        string assemblyName,
        MetadataReference domainReference,
        IReadOnlyDictionary<string, string> globalOptions)
    {
        var generatedSources = RunGeneratorSources(
            assemblyName,
            domainReference,
            globalOptions,
            "");

        var generatedSource = generatedSources.Single();
        generatedSource.Should().NotBeNullOrWhiteSpace();
        return generatedSource;
    }

    private static List<string> RunGeneratorSources(
        string assemblyName,
        MetadataReference domainReference,
        IReadOnlyDictionary<string, string> globalOptions,
        string projectSource)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(projectSource, parseOptions)],
            GetMetadataReferences().Append(domainReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var inputDiagnostics = compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        inputDiagnostics.Should().BeEmpty();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ServiceWrapperGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(globalOptions));

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out var generatorDiagnostics);

        generatorDiagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        return driver.GetRunResult()
            .GeneratedTrees
            .Where(tree => tree.FilePath.EndsWith("ServiceWrapperGenerator.g.cs", StringComparison.Ordinal))
            .Select(tree => tree.GetText().ToString())
            .ToList();
    }

    private static MetadataReference CreateReference(string assemblyName, string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [
                CSharpSyntaxTree.ParseText(CommonStubs, parseOptions),
                CSharpSyntaxTree.ParseText(source, parseOptions)
            ],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        emitResult.Diagnostics
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        emitResult.Success.Should().BeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(static reference => reference.Display);
    }

    private sealed class TestAnalyzerConfigOptionsProvider(
        IReadOnlyDictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
        private readonly AnalyzerConfigOptions _emptyOptions = new DictionaryAnalyzerConfigOptions(
            ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _emptyOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _emptyOptions;
    }

    private sealed class DictionaryAnalyzerConfigOptions(
        IReadOnlyDictionary<string, string> options) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (options.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private const string CommonStubs = """
    using System;

    namespace XFramework.Domain.Shared.Attributes
    {
        public enum EndpointType { Service = 1, Rest = 2, Both = 3 }

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
            public bool RequireAuthorization { get; set; }
            public string? AuthorizationFeature { get; set; }
            public string ReadCapability { get; set; } = "view";
            public string CreateCapability { get; set; } = "create";
            public string UpdateCapability { get; set; } = "update";
            public string DeleteCapability { get; set; } = "delete";
        }
    }

    namespace XFramework.Domain.Shared.Contracts.Base
    {
        public abstract class BaseModel
        {
            public Guid Id { get; set; }
            public Guid TenantId { get; set; }
        }

        public interface IHasRequestServer;
    }

    namespace XFramework.Domain.Shared.Contracts.Requests
    {
        using XFramework.Domain.Shared.Contracts.Base;

        public abstract record RequestBase : IHasRequestServer;
        public interface ICommand<TResponse> : IHasRequestServer;
        public interface IQuery<TResponse> : IHasRequestServer;
    }

    namespace XFramework.Domain.Shared.BusinessObjects
    {
        public class CmdResponse;
        public class CmdResponse<T> : CmdResponse;
        public class QueryResponse<T>;
    }

    namespace Bolt.Domain.Shared.Contracts.Requests
    {
        public interface IBoltRequest<TRequest, TResponse>;
    }
    """;
}
