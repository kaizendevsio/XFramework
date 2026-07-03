using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that creates client-side service wrapper interfaces and implementations
/// for cross-module Bolt calls. Auto-discovers entities from [GenerateEndpoints] attribute
/// in referenced assemblies and custom Bolt methods from IBoltRequest types.
/// No [BoltWrapper] attribute needed - derives everything from assembly name convention.
/// </summary>
[Generator]
public class ServiceWrapperGenerator : IIncrementalGenerator
{
    private const string WrapperNameProperty = "build_property.XFrameworkServiceWrapperName";
    private const string TargetClientNameProperty = "build_property.XFrameworkServiceWrapperTargetClientName";
    private const string DiscoveryPrefixesProperty = "build_property.XFrameworkServiceWrapperDiscoveryPrefixes";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pure auto-discovery: use CompilationProvider to scan referenced assemblies
        // for [GenerateEndpoints] entities and IBoltRequest types.
        // Triggers only in *.Integration projects (by assembly name convention).
        var source = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider);
        context.RegisterSourceOutput(source,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static void Execute(
        Compilation compilation,
        AnalyzerConfigOptionsProvider analyzerConfigOptions,
        SourceProductionContext context)
    {
        var assemblyName = compilation.AssemblyName ?? "";

        // Only run in Integration projects
        if (!assemblyName.EndsWith(".Integration"))
            return;

        var options = ResolveOptions(compilation, analyzerConfigOptions);
        var serviceId = options.TargetClientName.ToSha256();
        if (HasExistingServiceWrapperDeclaration(compilation, options.WrapperName))
            return;

        // Discover entities from [GenerateEndpoints] in referenced assemblies
        var (models, namespaces) = DiscoverGenerateEndpointEntities(compilation, options.DiscoveryPrefixes);

        // Also check for [BoltWrapper] for backward compatibility (entities not yet migrated)
        var (legacyModels, legacyNamespace) = DiscoverBoltWrapperEntities(compilation);
        foreach (var model in legacyModels)
        {
            if (!models.Contains(model))
                models.Add(model);
        }
        if (!string.IsNullOrEmpty(legacyNamespace))
            namespaces.Add(legacyNamespace);

        // Discover custom Bolt request types (IBoltRequest implementations)
        var customRequests = DiscoverBoltRequests(compilation, options.DiscoveryPrefixes);

        if (models.Count == 0 && customRequests.Count == 0)
            return;

        // Generate the wrapper
        var source = GenerateWrapper(options.WrapperName, serviceId, models, namespaces, customRequests);
        context.AddSource($"{options.WrapperName}ServiceWrapperGenerator.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    private static WrapperGenerationOptions ResolveOptions(
        Compilation compilation,
        AnalyzerConfigOptionsProvider analyzerConfigOptions)
    {
        var assemblyName = compilation.AssemblyName ?? "";
        var conventionalName = assemblyName.Split('.').FirstOrDefault() ?? "";

        var wrapperName = GetGlobalOption(analyzerConfigOptions, WrapperNameProperty);
        if (string.IsNullOrWhiteSpace(wrapperName))
            wrapperName = conventionalName;

        var targetClientName = GetGlobalOption(analyzerConfigOptions, TargetClientNameProperty);
        if (string.IsNullOrWhiteSpace(targetClientName))
            targetClientName = wrapperName;
        targetClientName = CanonicalizeClientName(targetClientName);

        var discoveryPrefixes = ParseDiscoveryPrefixes(
            GetGlobalOption(analyzerConfigOptions, DiscoveryPrefixesProperty));
        if (discoveryPrefixes.Count == 0)
            discoveryPrefixes.Add(wrapperName);

        return new WrapperGenerationOptions(wrapperName, targetClientName, discoveryPrefixes);
    }

    private static string CanonicalizeClientName(string clientName)
    {
        if (clientName.StartsWith("XFramework.", StringComparison.Ordinal))
            return clientName;

        return clientName switch
        {
            "Portal" => "XFramework.Portal",
            "OperationsDashboard" => "XFramework.Operations.Dashboard",
            _ => $"XFramework.{clientName}"
        };
    }

    private static string GetGlobalOption(
        AnalyzerConfigOptionsProvider analyzerConfigOptions,
        string key) =>
        analyzerConfigOptions.GlobalOptions.TryGetValue(key, out var value)
            ? value.Trim()
            : string.Empty;

    private static List<string> ParseDiscoveryPrefixes(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return [];

        return rawValue
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => value.Trim())
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasExistingServiceWrapperDeclaration(Compilation compilation, string wrapperName)
    {
        var interfaceName = $"I{wrapperName}ServiceWrapper";
        var recordName = $"{wrapperName}ServiceWrapper";
        var extensionsName = $"{wrapperName}ServiceWrapperExtensions";

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var identifier = declaration.Identifier.Text;
                if (identifier.Equals(interfaceName, StringComparison.Ordinal) ||
                    identifier.Equals(recordName, StringComparison.Ordinal) ||
                    identifier.Equals(extensionsName, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static string GenerateWrapper(string serviceName, string serviceId,
        List<string> models, HashSet<string> namespaces, List<CustomRequestInfo> customRequests)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $$"""
            // <auto-generated/>
            #nullable enable
            using XFramework.Domain.Shared.BusinessObjects;
            using XFramework.Domain.Shared.Contracts;
            using XFramework.Domain.Shared.Contracts.Requests;
            using XFramework.Domain.Shared.Contracts.Responses;
            using XFramework.Domain.Shared.Interfaces;
            using XFramework.Domain.Shared.DataContext;
            using XFramework.Integration.Drivers;
            using XFramework.Integration.Abstractions;
            using XFramework.Integration.Abstractions.Wrappers;
            using XFramework.Integration.Security;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Configuration;
            using Bolt.Client;
            using XFramework;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System;
            using System.Net;
            using Microsoft.Extensions.Logging;
            """);

        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {serviceName}.Integration.Drivers");
        sb.AppendLine("{");

        // -- Interface --
        sb.AppendLine($"public partial interface I{serviceName}ServiceWrapper : IXFrameworkService, IServiceWrapper, IDataContextServiceWrapper");
        sb.AppendLine("{");
        foreach (var model in models)
        {
            sb.AppendLine($"    public I{model}CrudService {model} {{ get; init; }}");
        }
        foreach (var req in customRequests)
        {
            sb.AppendLine($"    {req.InterfaceMethodSignature}");
        }
        sb.AppendLine("}");

        // -- CRUD service interfaces --
        foreach (var model in models)
        {
            sb.AppendLine($"public interface I{model}CrudService : ICrudService<{model}>;");
        }

        // -- ServiceWrapper record --
        sb.AppendLine($"public partial record {serviceName}ServiceWrapper(");
        var constructorParameters = models
            .Select(static model => $"I{model}CrudService {model}")
            .Concat([
                "IMessageBusWrapper messageBusDriver",
                "IConfiguration configuration",
                "BoltClient boltClient",
                "XFramework.Integration.Security.IServiceTokenProvider serviceTokenProvider"
            ])
            .ToList();
        for (int i = 0; i < constructorParameters.Count; i++)
        {
            sb.AppendLine($"{constructorParameters[i]}{(i < constructorParameters.Count - 1 ? "," : "")}");
        }
        sb.AppendLine($") : DriverBase(messageBusDriver, configuration), I{serviceName}ServiceWrapper");
        sb.AppendLine("{");
        sb.AppendLine($"    public override void Initialize() => TargetClient = \"{serviceId}\";");
        sb.AppendLine();
        sb.AppendLine($$"""
                            public async System.Threading.Tasks.Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, System.Threading.CancellationToken ct = default)
                            {
                                if (string.IsNullOrEmpty(TargetClient)) Initialize();
                                var targetClient = TargetClient ?? throw new System.InvalidOperationException("Target client was not initialized.");
                                var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((System.ReadOnlySpan<byte>)queryDescriptorBytes)
                                    ?? throw new System.InvalidOperationException("Query descriptor could not be deserialized.");
                                await descriptor.AttachServiceTokenAsync(serviceTokenProvider, XFramework.Integration.Security.ServiceTokenMetadataExtensions.ResolveCanonicalAudience(targetClient), ct);
                                var (status, data) = await boltClient.InvokeAsync(targetClient, "__db_query__", MemoryPack.MemoryPackSerializer.Serialize(descriptor), ct);
                                if ((int)status < 200 || (int)status >= 300)
                                    throw new System.InvalidOperationException($"DataContext query request failed with status {(int)status} ({status}).");

                                return data.ToArray();
                            }

                            public async System.Threading.Tasks.Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, System.Threading.CancellationToken ct = default)
                            {
                                if (string.IsNullOrEmpty(TargetClient)) Initialize();
                                var targetClient = TargetClient ?? throw new System.InvalidOperationException("Target client was not initialized.");
                                var request = MemoryPack.MemoryPackSerializer.Deserialize<SaveChangesRequest>((System.ReadOnlySpan<byte>)saveChangesRequestBytes)
                                    ?? throw new System.InvalidOperationException("SaveChanges request could not be deserialized.");
                                await request.AttachServiceTokenAsync(serviceTokenProvider, XFramework.Integration.Security.ServiceTokenMetadataExtensions.ResolveCanonicalAudience(targetClient), ct);
                                var (status, data) = await boltClient.InvokeAsync(targetClient, "__db_changes__", MemoryPack.MemoryPackSerializer.Serialize(request), ct);
                                if ((int)status < 200 || (int)status >= 300)
                                {
                                    var failure = DataContextResult.Failure($"DataContext change request failed with status {(int)status} ({status}).", (int)status);
                                    return MemoryPack.MemoryPackSerializer.Serialize(failure);
                                }

                                return data.ToArray();
                            }

                            public async System.Collections.Generic.IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(
                                byte[] queryDescriptorBytes,
                                [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct = default)
                            {
                                if (string.IsNullOrEmpty(TargetClient)) Initialize();
                                var targetClient = TargetClient ?? throw new System.InvalidOperationException("Target client was not initialized.");
                                var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((System.ReadOnlySpan<byte>)queryDescriptorBytes)
                                    ?? throw new System.InvalidOperationException("Query descriptor could not be deserialized.");
                                await descriptor.AttachServiceTokenAsync(serviceTokenProvider, XFramework.Integration.Security.ServiceTokenMetadataExtensions.ResolveCanonicalAudience(targetClient), ct);
                                var stream = await boltClient.OpenStreamAsync(targetClient, "__db_query_stream__", ct);
                                try
                                {
                                    await stream.SendAsync((System.ReadOnlyMemory<byte>)MemoryPack.MemoryPackSerializer.Serialize(descriptor), ct);
                                    await foreach (var chunk in stream.ReadAllAsync(ct))
                                    {
                                        yield return chunk.ToArray();
                                    }
                                }
                                finally
                                {
                                    await stream.DisposeAsync();
                                }
                            }
                        """);

        foreach (var req in customRequests)
        {
            sb.AppendLine();
            sb.AppendLine($"    {req.ImplementationMethod}");
        }
        sb.AppendLine("}");

        // -- CRUD service implementations --
        // Route through __db_query__ / __db_changes__ (same handlers as IDataContext).
        // Uses BoltClient.InvokeAsync directly - zero-copy deserialization from response span.
        foreach (var model in models)
        {
            sb.AppendLine(
                $$"""
                  public record {{model}}CrudService : I{{model}}CrudService, IServiceWrapper
                  {
                      private readonly BoltClient _boltClient;
                      private readonly ILogger _logger;
                      private readonly string _targetClient = "{{serviceId}}";
                      private readonly XFramework.Integration.Security.IServiceTokenProvider _serviceTokenProvider;

                      private static readonly JsonSerializerOptions _jsonOpts = new()
                      {
                          WriteIndented = false, MaxDepth = 4,
                          ReferenceHandler = ReferenceHandler.IgnoreCycles,
                          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                      };

                      private static JsonElement ToJson(object? obj)
                      {
                          if (obj is null) return default;
                          try { return JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(obj, _jsonOpts)).RootElement.Clone(); }
                          catch { return default; }
                      }

                      public {{model}}CrudService(BoltClient boltClient, ILoggerFactory loggerFactory, XFramework.Integration.Security.IServiceTokenProvider serviceTokenProvider)
                      {
                          _boltClient = boltClient;
                          _logger = loggerFactory.CreateLogger("Bolt.Crud.{{model}}");
                          _serviceTokenProvider = serviceTokenProvider;
                      }

                      public async Task<CmdResponse<{{model}}>> Create({{model}} entity)
                      {
                          var result = await ExecuteChange("{{model}}", ChangeOperation.Add, MemoryPack.MemoryPackSerializer.Serialize(entity), entity);
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = result.IsSuccess ? HttpStatusCode.OK : (HttpStatusCode)result.StatusCode,
                              Message = result.Message,
                              Response = entity
                          };
                      }

                      public async Task<CmdResponse<{{model}}>> Patch({{model}} entity)
                      {
                          var result = await ExecuteChange("{{model}}", ChangeOperation.Update, MemoryPack.MemoryPackSerializer.Serialize(entity), entity);
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = result.IsSuccess ? HttpStatusCode.OK : (HttpStatusCode)result.StatusCode,
                              Message = result.Message,
                              Response = entity
                          };
                      }

                      public async Task<CmdResponse<{{model}}>> Replace({{model}} entity)
                      {
                          var result = await ExecuteChange("{{model}}", ChangeOperation.Update, MemoryPack.MemoryPackSerializer.Serialize(entity), entity);
                          return new CmdResponse<{{model}}>
                          {
                              HttpStatusCode = result.IsSuccess ? HttpStatusCode.OK : (HttpStatusCode)result.StatusCode,
                              Message = result.Message,
                              Response = entity
                          };
                      }

                      public async Task<CmdResponse> Delete({{model}} entity)
                      {
                          var result = await ExecuteChange("{{model}}", ChangeOperation.Remove, MemoryPack.MemoryPackSerializer.Serialize(entity), entity);
                          return new CmdResponse
                          {
                              HttpStatusCode = result.IsSuccess ? HttpStatusCode.OK : (HttpStatusCode)result.StatusCode,
                              Message = result.Message
                          };
                      }

                      public async Task<QueryResponse<PaginatedResult<{{model}}>>> GetList(int pageSize, int pageNumber, Guid? tenantId = null, bool noCache = true, int navigationDepth = 1, bool? includeNavigations = false, List<QueryFilter>? filter = null, List<string>? includes = null)
                      {
                          var descriptor = new QueryDescriptor
                          {
                              EntityTypeName = "{{model}}",
                              Mode = QueryExecutionMode.ToList,
                              Skip = (pageNumber - 1) * pageSize,
                              Take = pageSize,
                              NoCache = noCache,
                              Includes = includes ?? new List<string>(),
                              Filters = filter ?? new List<QueryFilter>(),
                              Metadata = new RequestMetadata { TenantId = tenantId }
                          };
                          await descriptor.AttachServiceTokenAsync(_serviceTokenProvider, XFramework.Integration.Security.ServiceTokenMetadataExtensions.ResolveCanonicalAudience(_targetClient));
                          var (status, data) = await _boltClient.InvokeAsync(_targetClient, "__db_query__", MemoryPack.MemoryPackSerializer.Serialize(descriptor));
                          var items = data.IsEmpty ? new List<{{model}}>() : MemoryPack.MemoryPackSerializer.Deserialize<List<{{model}}>>(data.Span) ?? new List<{{model}}>();

                          _logger.LogDebug("GetList<{{model}}> | {StatusCode} in {Elapsed}ms | Request={Request} Response={Response}",
                              (int)status, 0,
                              ToJson(new { Size = descriptor.Take, Body = descriptor }),
                              ToJson(new { Status = (int)status, Count = items.Count, Body = items }));

                          return new QueryResponse<PaginatedResult<{{model}}>>
                          {
                              HttpStatusCode = status,
                              Response = new PaginatedResult<{{model}}>(items.Count, pageNumber, pageSize, items)
                          };
                      }

                      public async Task<QueryResponse<{{model}}>> Get(Guid id, Guid? tenantId = null, bool noCache = true, int navigationDepth = 1, bool? includeNavigations = null, List<string>? includes = null)
                      {
                          var descriptor = new QueryDescriptor
                          {
                              EntityTypeName = "{{model}}",
                              Mode = QueryExecutionMode.FirstOrDefault,
                              NoCache = noCache,
                              Includes = includes ?? new List<string>(),
                              Filters = new List<QueryFilter> { new() { PropertyName = "Id", Operation = global::XFramework.Domain.Shared.Enums.QueryFilterOperation.Equal, Value = id } },
                              Metadata = new RequestMetadata { TenantId = tenantId }
                          };
                          await descriptor.AttachServiceTokenAsync(_serviceTokenProvider, XFramework.Integration.Security.ServiceTokenMetadataExtensions.ResolveCanonicalAudience(_targetClient));
                          var (status, data) = await _boltClient.InvokeAsync(_targetClient, "__db_query__", MemoryPack.MemoryPackSerializer.Serialize(descriptor));
                          var entity = data.IsEmpty ? default : MemoryPack.MemoryPackSerializer.Deserialize<{{model}}>(data.Span);

                          _logger.LogDebug("Get<{{model}}> | {StatusCode} | Request={Request} Response={Response}",
                              (int)status,
                              ToJson(new { Body = descriptor }),
                              ToJson(new { Status = (int)status, Found = entity is not null, Body = entity }));

                          return new QueryResponse<{{model}}>
                          {
                              HttpStatusCode = status,
                              Response = entity
                          };
                      }

                      private async Task<DataContextResult> ExecuteChange(string entityType, ChangeOperation op, byte[] serializedEntity, object? entityForLog = null)
                      {
                          var request = new SaveChangesRequest
                          {
                              Changes = new List<ChangeEntry>
                              {
                                  new() { EntityTypeName = entityType, Operation = op, SerializedEntity = serializedEntity }
                              },
                              Metadata = BuildRequestMetadata(entityForLog)
                          };
                          await request.AttachServiceTokenAsync(_serviceTokenProvider, XFramework.Integration.Security.ServiceTokenMetadataExtensions.ResolveCanonicalAudience(_targetClient));
                          var (status, data) = await _boltClient.InvokeAsync(_targetClient, "__db_changes__", MemoryPack.MemoryPackSerializer.Serialize(request));
                          var result = data.IsEmpty
                              ? DataContextResult.Failure("Empty response", (int)status)
                              : MemoryPack.MemoryPackSerializer.Deserialize<DataContextResult>(data.Span) ?? DataContextResult.Failure("Deserialize failed");

                          var level = result.IsSuccess ? LogLevel.Debug : LogLevel.Warning;
                          _logger.Log(level, "{Operation}<{Entity}> | {StatusCode} | Request={Request} Response={Response}",
                              op, entityType, result.StatusCode,
                              ToJson(new { Body = entityForLog }),
                              ToJson(new { Status = result.StatusCode, Success = result.IsSuccess, Message = result.Message }));

                          return result;
                      }

                      private static RequestMetadata? BuildRequestMetadata(object? entityForLog)
                      {
                          var tenantIdProperty = entityForLog?.GetType().GetProperty("TenantId");
                          if (tenantIdProperty?.GetValue(entityForLog) is Guid tenantId && tenantId != Guid.Empty)
                          {
                              return new RequestMetadata { TenantId = tenantId };
                          }

                          return null;
                      }
                  }
                  """);
        }

        // -- DI Registration --
        sb.AppendLine($$"""
                         public static class {{serviceName}}ServiceWrapperExtensions
                         {
                             public static void Add{{serviceName}}WrapperServices(this IServiceCollection services)
                             {
                                     // Service wrapper registration
                                 services.AddSingleton<I{{serviceName}}ServiceWrapper, {{serviceName}}ServiceWrapper>();
                         """);
        foreach (var model in models)
        {
            sb.AppendLine($"        services.AddSingleton<I{model}CrudService>(sp => new {model}CrudService(sp.GetRequiredService<BoltClient>(), sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<XFramework.Integration.Security.IServiceTokenProvider>()));");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        sb.AppendLine("}"); // close namespace

        return sb.ToString();
    }

    private static (List<string> Models, HashSet<string> Namespaces) DiscoverGenerateEndpointEntities(
        Compilation compilation, IReadOnlyCollection<string> discoveryPrefixes)
    {
        var models = new List<string>();
        var namespaces = new HashSet<string>();
        var coreAttr = "XFramework.Core.Attributes.GenerateEndpointsAttribute";
        var sharedAttr = "XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute";

        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is not IAssemblySymbol assembly)
                continue;

            // Scan module's own assemblies
            // Also scan XFramework.Domain.Shared for IdentityServer (manages StorageFile etc.)
            var isModuleAssembly = MatchesAnyPrefix(assembly.Name, discoveryPrefixes);
            var isSharedForIdentity = discoveryPrefixes.Any(static prefix =>
                    prefix.Equals("IdentityServer", StringComparison.OrdinalIgnoreCase)) &&
                assembly.Name.StartsWith("XFramework.Domain.Shared", StringComparison.OrdinalIgnoreCase);
            if (!isModuleAssembly && !isSharedForIdentity)
                continue;

            foreach (var type in GetTypesFromNamespace(assembly.GlobalNamespace))
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

    /// <summary>
    /// Backward compatibility: discover entities from [BoltWrapper] if still present.
    /// </summary>
    private static (List<string> Models, string Namespace) DiscoverBoltWrapperEntities(
        Compilation compilation)
    {
        var models = new List<string>();
        var ns = "";

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
                    continue;

                foreach (var attr in classSymbol.GetAttributes())
                {
                    if (attr.AttributeClass?.Name != "BoltWrapperAttribute")
                        continue;

                    if (attr.ConstructorArguments.Length > 0)
                        ns = attr.ConstructorArguments[0].Value?.ToString() ?? "";

                    if (attr.ConstructorArguments.Length > 1)
                    {
                        foreach (var item in attr.ConstructorArguments[1].Values)
                        {
                            if (item.Value is string name && !models.Contains(name))
                                models.Add(name);
                        }
                    }
                }
            }
        }

        return (models, ns);
    }

    private static List<CustomRequestInfo> DiscoverBoltRequests(
        Compilation compilation, IReadOnlyCollection<string> discoveryPrefixes)
    {
        var results = new List<CustomRequestInfo>();
        var boltInterface = compilation.GetTypeByMetadataName(
            "Bolt.Domain.Shared.Contracts.Requests.IBoltRequest`2");

        if (boltInterface == null)
            return results;

        var allTypes = GetAllTypes(compilation);
        var seen = new HashSet<string>();

        foreach (var type in allTypes)
        {
            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                continue;

            var typeNamespace = type.ContainingNamespace?.ToDisplayString() ?? "";
            if (!MatchesAnyPrefix(typeNamespace, discoveryPrefixes))
                continue;

            if (type.IsGenericType) continue;

            foreach (var iface in type.AllInterfaces)
            {
                if (!iface.IsGenericType || iface.OriginalDefinition == null)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, boltInterface))
                    continue;

                var tRequest = iface.TypeArguments[0];
                var tResponse = iface.TypeArguments[1];
                var requestFullName = tRequest.ToDisplayString();

                var methodName = type.Name;
                if (methodName.EndsWith("Request"))
                    methodName = methodName.Substring(0, methodName.Length - "Request".Length);

                var isQueryResponse = tResponse.Name == "QueryResponse" &&
                                      tResponse is INamedTypeSymbol { IsGenericType: true };
                var isTypedCommandResponse = tResponse.Name == "CmdResponse" &&
                                             tResponse is INamedTypeSymbol { IsGenericType: true };

                string interfaceSig, implMethod;

                if (isQueryResponse)
                {
                    var innerType = ((INamedTypeSymbol)tResponse).TypeArguments[0].ToDisplayString();
                    interfaceSig = $"Task<QueryResponse<{innerType}>> {methodName}({requestFullName} request);";
                    implMethod = $"public Task<QueryResponse<{innerType}>> {methodName}({requestFullName} request) => SendAsync<{requestFullName}, {innerType}>(request);";
                }
                else if (isTypedCommandResponse)
                {
                    var innerType = ((INamedTypeSymbol)tResponse).TypeArguments[0].ToDisplayString();
                    interfaceSig = $"Task<CmdResponse<{innerType}>> {methodName}({requestFullName} request);";
                    implMethod = $"public Task<CmdResponse<{innerType}>> {methodName}({requestFullName} request) => SendVoidAsync<{requestFullName}, {innerType}>(request);";
                }
                else
                {
                    interfaceSig = $"Task<CmdResponse> {methodName}({requestFullName} request);";
                    implMethod = $"public Task<CmdResponse> {methodName}({requestFullName} request) => SendVoidAsync(request);";
                }

                if (!seen.Add(requestFullName))
                    break;

                results.Add(new CustomRequestInfo
                {
                    InterfaceMethodSignature = interfaceSig,
                    ImplementationMethod = implMethod
                });

                break;
            }
        }

        return results;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        var types = new List<INamedTypeSymbol>();
        CollectTypes(compilation.GlobalNamespace, types);
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                CollectTypes(assembly.GlobalNamespace, types);
        }
        return types;
    }

    private static IEnumerable<INamedTypeSymbol> GetTypesFromNamespace(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;
        foreach (var childNs in ns.GetNamespaceMembers())
            foreach (var type in GetTypesFromNamespace(childNs))
                yield return type;
    }

    private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> types)
    {
        types.AddRange(ns.GetTypeMembers());
        foreach (var childNs in ns.GetNamespaceMembers())
            CollectTypes(childNs, types);
    }

    private static bool MatchesAnyPrefix(string value, IReadOnlyCollection<string> prefixes) =>
        prefixes.Any(prefix => value.Contains(prefix, StringComparison.OrdinalIgnoreCase));

    private class CustomRequestInfo
    {
        public string InterfaceMethodSignature { get; set; } = "";
        public string ImplementationMethod { get; set; } = "";
    }

    private sealed class WrapperGenerationOptions
    {
        public WrapperGenerationOptions(
            string wrapperName,
            string targetClientName,
            List<string> discoveryPrefixes)
        {
            WrapperName = wrapperName;
            TargetClientName = targetClientName;
            DiscoveryPrefixes = discoveryPrefixes;
        }

        public string WrapperName { get; }
        public string TargetClientName { get; }
        public List<string> DiscoveryPrefixes { get; }
    }
}
