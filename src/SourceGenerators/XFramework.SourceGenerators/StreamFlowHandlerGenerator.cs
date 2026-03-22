using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that scans for [StreamFlowHandler] and [MapPost/Get/Put/Patch/Delete]
/// attributes on static methods and generates:
///
///   1. StreamFlow ISignalREventHandler — routes SignalR messages to the Handle method
///   2. REST adapter — converts Result&lt;T&gt; to HTTP status codes
///   3. Map extension method — registers the endpoint with ASP.NET routing
///   4. Auto-detected FluentValidation — runs validator before handler if one exists
///
/// All generated code is compile-time only. Zero runtime reflection. Zero allocations
/// on the hot path beyond what the handler itself does.
/// </summary>
[Generator]
public class StreamFlowHandlerGenerator : ISourceGenerator
{
    private static readonly string[] HttpMethodAttributes =
        { "MapPost", "MapGet", "MapPut", "MapPatch", "MapDelete" };

    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {
        var handlers = FindHandlers(context);
        if (handlers.Count == 0) return;

        foreach (var h in handlers)
        {
            if (h.HasStreamFlowHandler)
            {
                var sfSource = GenerateStreamFlowHandler(h);
                context.AddSource($"{h.ClassName}_{h.MethodName}_StreamFlowHandler.g.cs",
                    SourceText.From(sfSource, Encoding.UTF8));
            }

            if (h.HttpMethod != null)
            {
                var restSource = GenerateRestEndpoint(h);
                context.AddSource($"{h.ClassName}_{h.MethodName}_RestEndpoint.g.cs",
                    SourceText.From(restSource, Encoding.UTF8));
            }
        }

        // Registry for StreamFlow handlers
        var sfHandlers = handlers.Where(h => h.HasStreamFlowHandler).ToList();
        if (sfHandlers.Count > 0)
        {
            var regSource = GenerateRegistry(sfHandlers, context);
            context.AddSource("StreamFlowHandlerRegistration.g.cs",
                SourceText.From(regSource, Encoding.UTF8));
        }

        // Single MapAll extension method for REST endpoints
        var restHandlers = handlers.Where(h => h.HttpMethod != null).ToList();
        if (restHandlers.Count > 0)
        {
            var mapAllSource = GenerateMapAll(restHandlers, context);
            context.AddSource("GeneratedEndpointRoutes.g.cs",
                SourceText.From(mapAllSource, Encoding.UTF8));
        }
    }

    #region Discovery

    private List<HandlerInfo> FindHandlers(GeneratorExecutionContext context)
    {
        var handlers = new List<HandlerInfo>();

        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);

            foreach (var method in syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var attrs = method.AttributeLists.SelectMany(a => a.Attributes).ToList();

                var hasStreamFlow = attrs.Any(a => a.Name.ToString().Contains("StreamFlowHandler"));
                var httpAttr = attrs.FirstOrDefault(a =>
                    HttpMethodAttributes.Any(m => a.Name.ToString().Contains(m)));

                if (!hasStreamFlow && httpAttr == null) continue;

                var methodSymbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;
                if (methodSymbol == null || !methodSymbol.IsStatic || methodSymbol.Parameters.Length == 0)
                    continue;

                var info = BuildHandlerInfo(methodSymbol, httpAttr, hasStreamFlow, semanticModel);
                if (info != null)
                    handlers.Add(info);
            }
        }

        return handlers;
    }

    private HandlerInfo? BuildHandlerInfo(IMethodSymbol methodSymbol, AttributeSyntax? httpAttr,
        bool hasStreamFlow, SemanticModel semanticModel)
    {
        var containingClass = methodSymbol.ContainingType;
        var requestType = methodSymbol.Parameters[0].Type;

        // Unwrap return type: Task<Result<T>> or Task<Result>
        var returnType = methodSymbol.ReturnType as INamedTypeSymbol;
        if (returnType?.TypeArguments.Length == 0) return null;

        var innerType = returnType!.TypeArguments[0] as INamedTypeSymbol;
        if (innerType == null) return null;

        var isGenericResult = innerType.IsGenericType && innerType.Name == "Result";
        var resultDataType = isGenericResult && innerType.TypeArguments.Length > 0
            ? innerType.TypeArguments[0] : null;

        // StreamFlow response type from IStreamflowRequest<TReq, TResp>
        ITypeSymbol? sfResponseType = null;
        if (hasStreamFlow)
        {
            var sfInterface = requestType.AllInterfaces
                .FirstOrDefault(i => i.IsGenericType && i.Name == "IStreamflowRequest");
            if (sfInterface == null) return null;
            sfResponseType = sfInterface.TypeArguments[1];
        }

        // HTTP method info from attribute
        string? httpMethod = null;
        string? route = null;
        string[]? tags = null;
        string? summary = null;
        string? description = null;
        bool excludeFromOpenApi = false;

        if (httpAttr != null)
        {
            var attrName = httpAttr.Name.ToString();
            httpMethod = HttpMethodAttributes.FirstOrDefault(m => attrName.Contains(m));

            // Extract route from first constructor arg
            if (httpAttr.ArgumentList?.Arguments.Count > 0)
            {
                var routeArg = httpAttr.ArgumentList.Arguments[0];
                route = routeArg.Expression.NormalizeWhitespace().ToFullString().Trim('"');
            }

            // Extract named args
            foreach (var arg in httpAttr.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>())
            {
                if (arg.NameEquals == null) continue;
                var name = arg.NameEquals.Name.Identifier.Text;
                var value = arg.Expression.NormalizeWhitespace().ToFullString();

                switch (name)
                {
                    case "Summary":
                        summary = value.Trim('"');
                        break;
                    case "Description":
                        description = value.Trim('"');
                        break;
                    case "ExcludeFromOpenApi":
                        excludeFromOpenApi = value == "true";
                        break;
                    case "Tags":
                        tags = ParseStringArray(value);
                        break;
                }
            }
        }

        // Collect DI parameters (skip request + CancellationToken)
        var diParams = new List<(string TypeFullName, string Name, bool IsValidator)>();
        var hasCancellationToken = false;
        var requestTypeFullName = ToGlobalQualified(requestType.ToDisplayString());

        for (int i = 1; i < methodSymbol.Parameters.Length; i++)
        {
            var p = methodSymbol.Parameters[i];
            if (p.Type.ToDisplayString() == "System.Threading.CancellationToken")
            {
                hasCancellationToken = true;
                continue;
            }
            diParams.Add((ToGlobalQualified(p.Type.ToDisplayString()), p.Name, false));
        }

        // Check if a concrete validator for this specific request type exists
        var validatorInterface = $"FluentValidation.IValidator<{requestTypeFullName}>";
        var abstractValidatorType = semanticModel.Compilation.GetTypeByMetadataName("FluentValidation.AbstractValidator`1");
        var hasValidator = false;
        if (abstractValidatorType != null)
        {
            var targetValidator = abstractValidatorType.Construct(requestType);
            hasValidator = semanticModel.Compilation.SyntaxTrees
                .SelectMany(t => t.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Any(cls =>
                {
                    var symbol = semanticModel.Compilation.GetSemanticModel(cls.SyntaxTree).GetDeclaredSymbol(cls) as INamedTypeSymbol;
                    if (symbol == null) return false;
                    var baseType = symbol.BaseType;
                    while (baseType != null)
                    {
                        if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, abstractValidatorType)
                            && baseType.TypeArguments.Length == 1
                            && SymbolEqualityComparer.Default.Equals(baseType.TypeArguments[0], requestType))
                            return true;
                        baseType = baseType.BaseType;
                    }
                    return false;
                });
        }

        return new HandlerInfo
        {
            ClassName = containingClass.Name,
            ClassFullName = ToGlobalQualified(containingClass.ToDisplayString()),
            MethodName = methodSymbol.Name,
            RequestTypeFullName = requestTypeFullName,
            RequestTypeName = requestType.Name,
            SfResponseTypeFullName = sfResponseType != null ? ToGlobalQualified(sfResponseType.ToDisplayString()) : null,
            ResultDataTypeFullName = resultDataType != null ? ToGlobalQualified(resultDataType.ToDisplayString()) : null,
            IsGenericResult = isGenericResult,
            DiParameters = diParams,
            HasCancellationToken = hasCancellationToken,
            Namespace = containingClass.ContainingNamespace.ToDisplayString(),
            HasStreamFlowHandler = hasStreamFlow,
            HttpMethod = httpMethod,
            Route = route,
            Tags = tags,
            Summary = summary,
            Description = description,
            ExcludeFromOpenApi = excludeFromOpenApi,
            ValidatorTypeFullName = hasValidator ? validatorInterface : null
        };
    }

    private static string[] ParseStringArray(string value)
    {
        // Parse: new[] { "Auth", "Admin" } or ["Auth", "Admin"]
        return value
            .Replace("new[]", "").Replace("new string[]", "")
            .Trim('{', '}', '[', ']', ' ')
            .Split(',')
            .Select(s => s.Trim().Trim('"'))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
    }

    /// <summary>
    /// Prefixes a fully-qualified type name with "global::" to avoid
    /// C# namespace resolution ambiguities in generated code.
    /// Handles generic types like "FluentValidation.IValidator&lt;Foo.Bar&gt;".
    /// </summary>
    private static string ToGlobalQualified(string typeName)
    {
        if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("global::"))
            return typeName;

        // Don't prefix built-in keywords or types without a namespace
        if (!typeName.Contains('.'))
            return typeName;

        // Handle generic types: "A.B<C.D, E.F>" → "global::A.B<global::C.D, global::E.F>"
        var angleBracket = typeName.IndexOf('<');
        if (angleBracket >= 0)
        {
            var outerType = typeName.Substring(0, angleBracket);
            var inner = typeName.Substring(angleBracket + 1, typeName.Length - angleBracket - 2);

            // Split type arguments respecting nested generics
            var typeArgs = SplitTypeArguments(inner);
            var qualifiedArgs = string.Join(", ", typeArgs.Select(ToGlobalQualified));
            return $"global::{outerType}<{qualifiedArgs}>";
        }

        return $"global::{typeName}";
    }

    private static List<string> SplitTypeArguments(string typeArgs)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (int i = 0; i < typeArgs.Length; i++)
        {
            switch (typeArgs[i])
            {
                case '<': depth++; break;
                case '>': depth--; break;
                case ',' when depth == 0:
                    result.Add(typeArgs.Substring(start, i - start).Trim());
                    start = i + 1;
                    break;
            }
        }
        result.Add(typeArgs.Substring(start).Trim());
        return result;
    }

    #endregion

    #region StreamFlow Handler Generation

    private string GenerateStreamFlowHandler(HandlerInfo h)
    {
        var diResolves = new StringBuilder();
        var callArgs = new StringBuilder("request");

        foreach (var (typeFullName, name, _) in h.DiParameters)
        {
            diResolves.AppendLine(
                $"                    var @{name} = scope.ServiceProvider.GetRequiredService<{typeFullName}>();");
            callArgs.Append($", @{name}");
        }

        if (h.HasCancellationToken)
            callArgs.Append(", CancellationToken.None");

        var isQueryResponse = h.SfResponseTypeFullName?.Contains("QueryResponse") == true;
        string resultConversion;
        if (isQueryResponse && h.IsGenericResult && h.ResultDataTypeFullName != null)
        {
            resultConversion = $@"                    var sfResponse = new {h.SfResponseTypeFullName}();
                    sfResponse.HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode;
                    if (result.IsSuccess)
                        sfResponse.Response = result.Data;
                    else
                        sfResponse.Message = result.Message;";
        }
        else
        {
            resultConversion = $@"                    var sfResponse = new {h.SfResponseTypeFullName}();
                    sfResponse.HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode;
                    sfResponse.Message = result.Message;";
        }

        return $@"// <auto-generated/>
#nullable enable
using System;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Drivers;
using XFramework.Integration.Services.Helpers;
using StreamFlow.Domain.Shared.Contracts.Requests;

namespace {h.Namespace}.Generated;

public sealed class {h.ClassName}_{h.MethodName}_StreamFlowHandler : BaseSignalRHandler, ISignalREventHandler
{{
    public void Handle(HubConnection connection, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
    {{
        logger.LogInformation(""Registering StreamFlow handler for {{RequestType}} -> {{Endpoint}}"",
            ""{h.RequestTypeName}"", ""{h.ClassFullName}.{h.MethodName}"");

        connection.On(typeof({h.RequestTypeFullName}).GetTypeFullName(),
            async (StreamFlowMessage<{h.RequestTypeFullName}> message) =>
            {{
                try
                {{
                    using var scope = scopeFactory.CreateScope();
                    var request = message.Data.AsCommandQuery<{h.RequestTypeFullName}>();
{diResolves}
                    var result = await {h.ClassFullName}.{h.MethodName}({callArgs});

{resultConversion}

                    await RespondToInvoke(connection, message.RequestId, message.ClientId, sfResponse);
                }}
                catch (Exception ex)
                {{
                    logger.LogError(ex, ""StreamFlow handler error for {{RequestType}}"", ""{h.RequestTypeName}"");
                    var err = new {h.SfResponseTypeFullName}();
                    err.HttpStatusCode = HttpStatusCode.InternalServerError;
                    err.Message = ""An error occurred while processing the request"";
                    await RespondToInvoke(connection, message.RequestId, message.ClientId, err);
                }}
                finally
                {{
                    message.Dispose();
                }}
            }});
    }}
}}";
    }

    #endregion

    #region REST Endpoint Generation

    private string GenerateRestEndpoint(HandlerInfo h)
    {
        var httpMapMethod = h.HttpMethod switch
        {
            "MapPost" => "MapPost",
            "MapGet" => "MapGet",
            "MapPut" => "MapPut",
            "MapPatch" => "MapPatch",
            "MapDelete" => "MapDelete",
            _ => "MapPost"
        };

        // Build the endpoint chain (.WithTags, .WithOpenApi, .ExcludeFromDescription)
        var chain = new StringBuilder();
        if (h.Tags?.Length > 0)
            chain.AppendLine($"            .WithTags({string.Join(", ", h.Tags.Select(t => $"\"{t}\""))})");
        if (h.Summary != null || h.Description != null)
        {
            chain.AppendLine("            .WithOpenApi(op =>");
            chain.AppendLine("            {");
            if (h.Summary != null)
                chain.AppendLine($"                op.Summary = \"{h.Summary}\";");
            if (h.Description != null)
                chain.AppendLine($"                op.Description = \"{h.Description}\";");
            chain.AppendLine("                return op;");
            chain.AppendLine("            })");
        }
        if (h.ExcludeFromOpenApi)
            chain.AppendLine("            .ExcludeFromDescription()");

        // Build REST handler parameters
        var restParams = new StringBuilder();
        var callArgs = new StringBuilder("request");

        // Request parameter — GET/DELETE use [AsParameters] for query binding, POST/PUT/PATCH use body
        var isBodylessMethod = httpMapMethod is "MapGet" or "MapDelete";
        var paramAttribute = isBodylessMethod ? "[AsParameters] " : "";
        restParams.Append($"{paramAttribute}{h.RequestTypeFullName} request");

        // DI service parameters
        foreach (var (typeFullName, name, _) in h.DiParameters)
        {
            restParams.Append($", {typeFullName} @{name}");
            callArgs.Append($", @{name}");
        }

        // Validator parameter (auto-detected)
        var hasValidator = h.ValidatorTypeFullName != null;
        if (hasValidator)
            restParams.Append($", {h.ValidatorTypeFullName} validator");

        // CancellationToken
        restParams.Append(", CancellationToken ct");
        if (h.HasCancellationToken)
            callArgs.Append(", ct");

        // Build validation block
        var validationBlock = "";
        if (hasValidator)
        {
            validationBlock = @"
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(static e => e.PropertyName)
                    .ToDictionary(static g => g.Key, static g => g.Select(static e => e.ErrorMessage).ToArray());
                return TypedResults.ValidationProblem(errors);
            }
";
        }

        // Build result type and conversion
        string resultTypes;
        string successReturn;
        string errorConversion;

        if (h.IsGenericResult && h.ResultDataTypeFullName != null)
        {
            var validationPart = hasValidator ? "ValidationProblem, " : "";
            resultTypes = $"Results<Ok<{h.ResultDataTypeFullName}>, {validationPart}NotFound, ProblemHttpResult>";
            successReturn = $"TypedResults.Ok(result.Data!)";
            errorConversion = @"return result.StatusCode switch
                {
                    401 => TypedResults.Problem(detail: result.Message, statusCode: 401),
                    403 => TypedResults.Problem(detail: result.Message, statusCode: 403),
                    404 => TypedResults.NotFound(),
                    _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
                };";
        }
        else
        {
            var validationPart = hasValidator ? "ValidationProblem, " : "";
            resultTypes = $"Results<Ok<string>, {validationPart}NotFound, ProblemHttpResult>";
            successReturn = "TypedResults.Ok(result.Message ?? \"Success\")";
            errorConversion = @"return result.StatusCode switch
                {
                    404 => TypedResults.NotFound(),
                    _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
                };";
        }

        var endpointName = h.ClassName.Replace("Endpoint", "");

        return $@"// <auto-generated/>
#nullable enable
using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace {h.Namespace}.Generated;

public static class {h.ClassName}_RestEndpoint
{{
    /// <summary>
    /// Maps the REST endpoint for {h.ClassName}.{h.MethodName}
    /// </summary>
    public static IEndpointRouteBuilder Map{endpointName}(this IEndpointRouteBuilder app)
    {{
        app.{httpMapMethod}(""{h.Route}"", RestHandle)
            .WithName(""{endpointName}"")
{chain}            ;

        return app;
    }}

    private static async Task<{resultTypes}> RestHandle({restParams})
    {{{validationBlock}
        var result = await {h.ClassFullName}.{h.MethodName}({callArgs});

        if (!result.IsSuccess)
        {{
            {errorConversion}
        }}

        return {successReturn};
    }}
}}";
    }

    #endregion

    #region Registration Generation

    private string GenerateRegistry(List<HandlerInfo> handlers, GeneratorExecutionContext context)
    {
        var namespaces = handlers.Select(h => h.Namespace + ".Generated").Distinct().ToList();
        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));
        var assemblyName = context.Compilation.AssemblyName ?? "Unknown";
        var entries = string.Join("\n", handlers.Select(h =>
            $"        typeof({h.ClassName}_{h.MethodName}_StreamFlowHandler),"));

        return $@"// <auto-generated/>
#nullable enable
using System;
using System.Collections.Generic;
{usings}

namespace {assemblyName}.Generated;

public static class StreamFlowHandlerRegistry
{{
    public static IReadOnlyList<Type> HandlerTypes {{ get; }} = new[]
    {{
{entries}
    }};
}}";
    }

    private string GenerateMapAll(List<HandlerInfo> handlers, GeneratorExecutionContext context)
    {
        var namespaces = handlers.Select(h => h.Namespace + ".Generated").Distinct().ToList();
        var usings = string.Join("\n", namespaces.Select(ns => $"using {ns};"));
        var assemblyName = context.Compilation.AssemblyName ?? "Unknown";

        var mapCalls = string.Join("\n", handlers.Select(h =>
        {
            var endpointName = h.ClassName.Replace("Endpoint", "");
            return $"        {h.ClassName}_RestEndpoint.Map{endpointName}(app);";
        }));

        return $@"// <auto-generated/>
#nullable enable
using Microsoft.AspNetCore.Routing;
{usings}

namespace {assemblyName}.Generated;

public static class GeneratedEndpointRoutes
{{
    /// <summary>
    /// Maps all source-generated REST endpoints.
    /// Call this from Program.cs: app.MapGeneratedEndpoints();
    /// </summary>
    public static IEndpointRouteBuilder MapGeneratedEndpoints(this IEndpointRouteBuilder app)
    {{
{mapCalls}
        return app;
    }}
}}";
    }

    #endregion

    #region Types

    private class HandlerInfo
    {
        public string ClassName { get; set; } = "";
        public string ClassFullName { get; set; } = "";
        public string MethodName { get; set; } = "";
        public string RequestTypeFullName { get; set; } = "";
        public string RequestTypeName { get; set; } = "";
        public string? SfResponseTypeFullName { get; set; }
        public string? ResultDataTypeFullName { get; set; }
        public bool IsGenericResult { get; set; }
        public List<(string TypeFullName, string Name, bool IsValidator)> DiParameters { get; set; } = new();
        public bool HasCancellationToken { get; set; }
        public string Namespace { get; set; } = "";

        // StreamFlow
        public bool HasStreamFlowHandler { get; set; }

        // REST
        public string? HttpMethod { get; set; }
        public string? Route { get; set; }
        public string[]? Tags { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public bool ExcludeFromOpenApi { get; set; }

        // Validation
        public string? ValidatorTypeFullName { get; set; }
    }

    #endregion
}
