using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

[Generator]
public class ServiceWrapperGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var assemblyName = context.Compilation.AssemblyName;
        var serviceName = assemblyName!.Contains('.')
            ? assemblyName.Split('.').First()
            : assemblyName;

        var namespaceName = BaseSourceGenerator.GetNamespace(context, "StreamFlowWrapper");
        var classes = BaseSourceGenerator.GetClasses(context, "StreamFlowWrapper", "ServiceWrapper");
        var serviceId = serviceName.ToSha256();

        foreach (var item in classes)
        {
            Generate(context, namespaceName, serviceId, item.ClassDeclarationSyntax, item.SemanticModel);
        }
    }

    private static void Generate(GeneratorExecutionContext context, string? namespaceName, string serviceId,
        ClassDeclarationSyntax classDeclarationSyntax, SemanticModel semanticModel)
    {
        var models = BaseSourceGenerator.GetModels(classDeclarationSyntax, "StreamFlowWrapper");
        var codeBuilder = new StringBuilder();
        var serviceName = classDeclarationSyntax.Identifier.Text.Replace("ServiceWrapper", string.Empty);

        // Also discover entities from [GenerateEndpoints] in referenced assemblies
        var (generatedModels, generatedNamespaces) = DiscoverGenerateEndpointEntities(context, serviceName);
        foreach (var model in generatedModels)
        {
            if (!models.Contains(model))
                models.Add(model);
        }

        if (models.Count == 0)
        {
            return;
        }

        // Discover custom StreamFlow request types from referenced assemblies
        var customRequests = DiscoverStreamFlowRequests(context, serviceName);

        codeBuilder.AppendLine(
            $$"""
            using XFramework.Domain.Shared.BusinessObjects;
            using XFramework.Domain.Shared.Contracts;
            using XFramework.Domain.Shared.Contracts.Requests;
            using XFramework.Domain.Shared.Contracts.Responses;
            using XFramework.Domain.Shared.Interfaces;
            using XFramework.Integration.Drivers;
            using XFramework.Integration.Abstractions;
            using XFramework.Integration.Abstractions.Wrappers;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Configuration;
            using XFramework;
            using System.Linq.Expressions;
            using Serilog;
            using System;
            using System.Net;


            namespace {{serviceName}}.Integration.Drivers
            {
            using {{namespaceName}};
            """);

        // Add usings for namespaces of [GenerateEndpoints] entities
        foreach (var ns in generatedNamespaces)
        {
            if (ns != namespaceName)
            {
                codeBuilder.AppendLine($"using {ns};");
            }
        }

        // ── Interface: CRUD properties + custom StreamFlow methods ──
        codeBuilder.AppendLine(
            $$"""
            public partial interface I{{serviceName}}ServiceWrapper : IXFrameworkService, IServiceWrapper
            {
            """);
        foreach (var model in models)
        {
            codeBuilder.AppendLine(
                $$"""
                    public I{{model}}CrudService {{model}} { get; init; }
                """);
        }

        // Add custom StreamFlow method signatures to interface
        foreach (var req in customRequests)
        {
            codeBuilder.AppendLine($"    {req.InterfaceMethodSignature}");
        }

        codeBuilder.AppendLine("            }");

        // ── CRUD service interfaces ──
        foreach (var model in models)
        {
            codeBuilder.AppendLine($"public interface I{model}CrudService : ICrudService<{model}>;");
        }

        // ── ServiceWrapper record (constructor + custom method implementations) ──
        codeBuilder.AppendLine($"public partial record {serviceName}ServiceWrapper(");
        foreach (var model in models)
        {
            codeBuilder.AppendLine($"I{model}CrudService {model}{(models.Last() == model ? "" : ",")}")
                ;
        }

        codeBuilder.AppendLine(
            $"{(models.Any() ? "," : string.Empty)} IMessageBusWrapper messageBusDriver, IConfiguration configuration");
        codeBuilder.AppendLine($") : DriverBase(messageBusDriver, configuration), I{serviceName}ServiceWrapper");

        codeBuilder.AppendLine($$"""
                                 {
                                     public override void Initialize()
                                     {
                                         TargetClient = "{{serviceId}}";
                                     }
                                 """);

        // Add custom StreamFlow method implementations inside the record
        foreach (var req in customRequests)
        {
            codeBuilder.AppendLine();
            codeBuilder.AppendLine($"    {req.ImplementationMethod}");
        }

        codeBuilder.AppendLine("}"); // close record

        // ── CRUD service implementations ──
        foreach (var model in models)
        {
            codeBuilder.AppendLine(
                $$"""
                  public record {{model}}CrudService : DriverBase, I{{model}}CrudService, IServiceWrapper
                  {
                      public {{model}}CrudService(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
                      {
                           MessageBusDriver = messageBusDriver;
                           Configuration = configuration;
                           TargetClient = "{{serviceId}}";
                      }

                      public async Task<CmdResponse<{{model}}>> Create({{model}} entity)
                      {
                          var t = await SendVoidAsync<Create<{{model}}>, {{model}}>(new Create<{{model}}>(entity));
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message,
                              Response = t?.Response
                          };
                      }

                      public async Task<CmdResponse<{{model}}>> Patch({{model}} entity)
                      {
                          var t = await SendVoidAsync<Patch<{{model}}>, {{model}}>(new Patch<{{model}}>(entity));
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message,
                              Response = t?.Response
                          };
                      }

                      public async Task<CmdResponse<{{model}}>> Replace({{model}} entity)
                      {
                          var t = await SendVoidAsync<Replace<{{model}}>, {{model}}>(new Replace<{{model}}>(entity));
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message,
                              Response = t?.Response
                          };
                      }

                      public async Task<CmdResponse> Delete({{model}} entity)
                      {
                          var t = await SendVoidAsync(new Delete<{{model}}>(entity));
                          return new CmdResponse
                          {
                              HttpStatusCode = t?.HttpStatusCode ?? HttpStatusCode.InternalServerError,
                              Message = t?.Message
                          };
                      }

                      public async Task<QueryResponse<PaginatedResult<{{model}}>>> GetList(
                        int pageSize,
                        int pageNumber,
                        Guid? tenantId = null,
                        bool noCache = true,
                        int navigationDepth = 1,
                        bool? includeNavigations = false,
                        List<QueryFilter>? filter = null,
                        List<string>? includes = null)
                      {
                          return await SendAsync<GetList<{{model}}>, PaginatedResult<{{model}}>>(new GetList<{{model}}>(
                            PageSize: pageSize,
                            PageNumber: pageNumber,
                            TenantId: tenantId,
                            NoCache: noCache,
                            IncludeNavigations: includeNavigations,
                            NavigationDepth: navigationDepth,
                            Filter: filter,
                            Includes: includes
                            ));
                      }

                      public async Task<QueryResponse<{{model}}>> Get(
                        Guid id,
                        Guid? tenantId = null,
                        bool noCache = true,
                        int navigationDepth = 1,
                        bool? includeNavigations = null,
                        List<string>? includes = null)
                      {
                          return await SendAsync<Get<{{model}}>, {{model}}>(new Get<{{model}}>(
                            Id: id,
                            TenantId: tenantId,
                            NoCache: noCache,
                            IncludeNavigations: includeNavigations,
                            NavigationDepth: navigationDepth,
                            Includes: includes
                            ));
                      }
                  }
                  """);
        }

        // ── DI Registration Extension ──
        codeBuilder.AppendLine($$"""

                                 public static class {{serviceName}}ServiceWrapperExtensions
                                 {

                                 public static void Add{{serviceName}}WrapperServices(this IServiceCollection services)
                                  {
                                      Serilog.Log.Information("Registering {{serviceName}}ServiceWrapper services");
                                      services.AddSingleton<I{{serviceName}}ServiceWrapper, {{serviceName}}ServiceWrapper>();
                                 """);

        foreach (var model in models)
        {
            codeBuilder.AppendLine($$"""

                                     services.AddSingleton<I{{model}}CrudService, {{model}}CrudService>();

                                     """);
        }

        codeBuilder.AppendLine("}}");
        codeBuilder.AppendLine("}");

        context.AddSource($"{serviceName}ServiceWrapperGenerator.g.cs",
            SourceText.From(codeBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Scans all referenced assemblies for types implementing IStreamflowRequest&lt;TRequest, TResponse&gt;
    /// that belong to this service's domain (namespace contains the service name).
    /// Returns metadata for generating wrapper methods.
    /// </summary>
    private static List<CustomRequestInfo> DiscoverStreamFlowRequests(
        GeneratorExecutionContext context, string serviceName)
    {
        var results = new List<CustomRequestInfo>();
        var streamflowInterface = context.Compilation.GetTypeByMetadataName(
            "StreamFlow.Domain.Shared.Contracts.Requests.IStreamflowRequest`2");

        if (streamflowInterface == null)
            return results;

        // Scan all types in the compilation and referenced assemblies
        var allTypes = GetAllTypes(context.Compilation);

        var seen = new HashSet<string>();

        foreach (var type in allTypes)
        {
            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                continue;

            // Check if this type's namespace contains the service name (e.g., "IdentityServer")
            var typeNamespace = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!typeNamespace.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip generic CRUD request types (Create<T>, Patch<T>, etc.)
            if (type.Name.StartsWith("Create") && type.IsGenericType) continue;
            if (type.Name.StartsWith("Patch") && type.IsGenericType) continue;
            if (type.Name.StartsWith("Delete") && type.IsGenericType) continue;
            if (type.Name.StartsWith("Replace") && type.IsGenericType) continue;
            if (type.Name.StartsWith("Get") && type.IsGenericType) continue;
            if (type.Name.StartsWith("GetList") && type.IsGenericType) continue;

            // Find IStreamflowRequest<TRequest, TResponse> implementation
            foreach (var iface in type.AllInterfaces)
            {
                if (!iface.IsGenericType || iface.OriginalDefinition == null)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, streamflowInterface))
                    continue;

                var tRequest = iface.TypeArguments[0];
                var tResponse = iface.TypeArguments[1];

                var requestFullName = tRequest.ToDisplayString();
                var responseFullName = tResponse.ToDisplayString();

                // Determine method name from request type name
                // e.g., "AuthenticateIdentityRequest" → "AuthenticateIdentity"
                var methodName = type.Name;
                if (methodName.EndsWith("Request"))
                    methodName = methodName.Substring(0, methodName.Length - "Request".Length);

                // Determine if QueryResponse<T> or CmdResponse
                var isQueryResponse = tResponse.Name == "QueryResponse" &&
                                      tResponse is INamedTypeSymbol { IsGenericType: true } namedResponse;

                string interfaceSig;
                string implMethod;

                if (isQueryResponse)
                {
                    var innerType = ((INamedTypeSymbol)tResponse).TypeArguments[0].ToDisplayString();
                    interfaceSig = $"Task<QueryResponse<{innerType}>> {methodName}({requestFullName} request);";
                    implMethod = $$"""
                        public Task<QueryResponse<{{innerType}}>> {{methodName}}({{requestFullName}} request)
                            {
                                return SendAsync<{{requestFullName}}, {{innerType}}>(request);
                            }
                        """;
                }
                else
                {
                    // CmdResponse (non-generic)
                    interfaceSig = $"Task<CmdResponse> {methodName}({requestFullName} request);";
                    implMethod = $$"""
                        public Task<CmdResponse> {{methodName}}({{requestFullName}} request)
                            {
                                return SendVoidAsync(request);
                            }
                        """;
                }

                        // Deduplicate — same type found from compilation + referenced assembly
                if (!seen.Add(requestFullName))
                    break;

                results.Add(new CustomRequestInfo
                {
                    MethodName = methodName,
                    RequestTypeFullName = requestFullName,
                    ResponseTypeFullName = responseFullName,
                    InterfaceMethodSignature = interfaceSig,
                    ImplementationMethod = implMethod
                });

                break; // Only process first IStreamflowRequest interface per type
            }
        }

        return results;
    }

    /// <summary>
    /// Gets all named types from the compilation and all referenced assemblies.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        var types = new List<INamedTypeSymbol>();
        CollectTypes(compilation.GlobalNamespace, types);

        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is IAssemblySymbol assembly)
            {
                CollectTypes(assembly.GlobalNamespace, types);
            }
        }

        return types;
    }

    private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> types)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            types.Add(type);
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            CollectTypes(childNs, types);
        }
    }

    /// <summary>
    /// Discovers entities with [GenerateEndpoints] attribute from referenced assemblies
    /// that belong to this module (filtered by service name prefix).
    /// Returns entity names and their namespaces.
    /// </summary>
    private static (List<string> Models, HashSet<string> Namespaces) DiscoverGenerateEndpointEntities(
        GeneratorExecutionContext context, string serviceName)
    {
        var models = new List<string>();
        var namespaces = new HashSet<string>();
        var coreAttr = "XFramework.Core.Attributes.GenerateEndpointsAttribute";
        var sharedAttr = "XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute";

        foreach (var reference in context.Compilation.References)
        {
            var symbol = context.Compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is not IAssemblySymbol assembly)
                continue;

            // Only scan assemblies belonging to this module
            if (!assembly.Name.StartsWith(serviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(assembly.GlobalNamespace, allTypes);

            foreach (var type in allTypes)
            {
                if (type.IsAbstract || type.TypeKind != TypeKind.Class)
                    continue;

                foreach (var attr in type.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();
                    if (attrName != coreAttr && attrName != sharedAttr)
                        continue;

                    models.Add(type.Name);
                    namespaces.Add(type.ContainingNamespace.ToDisplayString());
                    break;
                }
            }
        }

        return (models, namespaces);
    }

    private class CustomRequestInfo
    {
        public string MethodName { get; set; } = "";
        public string RequestTypeFullName { get; set; } = "";
        public string ResponseTypeFullName { get; set; } = "";
        public string InterfaceMethodSignature { get; set; } = "";
        public string ImplementationMethod { get; set; } = "";
    }
}
