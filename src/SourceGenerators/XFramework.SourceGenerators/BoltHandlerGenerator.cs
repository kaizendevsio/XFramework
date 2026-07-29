using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that scans for [BoltHandler] and [MapPost/Get/Put/Patch/Delete]
/// attributes on static methods and generates:
///
///   1. Bolt IBoltHandler — registers a MemoryPack-based callback on BoltClient via RegisterHandler
///   2. REST adapter — converts Result&lt;T&gt; to HTTP status codes
///   3. Map extension method — registers the endpoint with ASP.NET routing
///   4. Auto-detected FluentValidation — runs validator before handler if one exists
///
/// All generated code is compile-time only. Zero runtime reflection. Zero allocations
/// on the hot path beyond what the handler itself does.
/// </summary>
[Generator]
public class BoltHandlerGenerator : ISourceGenerator
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
            if (h.HasBoltHandler)
            {
                var sfSource = GenerateBoltHandler(h);
                context.AddSource($"{h.ClassName}_{h.MethodName}_BoltHandler.g.cs",
                    SourceText.From(sfSource, Encoding.UTF8));
            }

            if (h.HttpMethod != null)
            {
                var restSource = GenerateRestEndpoint(h);
                context.AddSource($"{h.ClassName}_{h.MethodName}_RestEndpoint.g.cs",
                    SourceText.From(restSource, Encoding.UTF8));
            }
        }

        // Registry for Bolt handlers
        var sfHandlers = handlers.Where(h => h.HasBoltHandler).ToList();
        if (sfHandlers.Count > 0)
        {
            var regSource = GenerateRegistry(sfHandlers, context);
            context.AddSource("BoltHandlerRegistration.g.cs",
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

                var hasBolt = attrs.Any(a => a.Name.ToString().Contains("BoltHandler"));
                var httpAttr = attrs.FirstOrDefault(a =>
                    HttpMethodAttributes.Any(m => a.Name.ToString().Contains(m)));

                if (!hasBolt && httpAttr == null) continue;

                var methodSymbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;
                if (methodSymbol == null || !methodSymbol.IsStatic || methodSymbol.Parameters.Length == 0)
                    continue;

                var info = BuildHandlerInfo(methodSymbol, httpAttr, hasBolt, semanticModel);
                if (info != null)
                    handlers.Add(info);
            }
        }

        return handlers;
    }

    private HandlerInfo? BuildHandlerInfo(IMethodSymbol methodSymbol, AttributeSyntax? httpAttr,
        bool hasBolt, SemanticModel semanticModel)
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

        // Bolt response type from IBoltRequest<TReq, TResp>
        ITypeSymbol? sfResponseType = null;
        if (hasBolt)
        {
            var sfInterface = requestType.AllInterfaces
                .FirstOrDefault(i => i.IsGenericType && i.Name == "IBoltRequest");
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
        bool requireAuthorization = false;
        string? authorizationPolicy = null;
        string[]? roles = null;
        string[]? requiredServiceScopes = null;
        string[]? allowedServiceCallers = null;

        if (hasBolt)
        {
            var boltAttributeData = methodSymbol.GetAttributes()
                .FirstOrDefault(static attribute =>
                    attribute.AttributeClass?.Name == "BoltHandlerAttribute");
            if (boltAttributeData != null)
            {
                requiredServiceScopes = GetStringArrayNamedArgument(
                    boltAttributeData,
                    "RequiredServiceScopes");
                allowedServiceCallers = GetStringArrayNamedArgument(
                    boltAttributeData,
                    "AllowedServiceCallers");
            }
        }

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
                    case "RequireAuthorization":
                        requireAuthorization = value == "true";
                        break;
                    case "AuthorizationPolicy":
                        authorizationPolicy = value.Trim('"');
                        break;
                    case "Tags":
                        tags = ParseStringArray(value);
                        break;
                    case "Roles":
                        roles = ParseStringArray(value);
                        break;
                }
            }

            var httpAttributeData = httpMethod == null
                ? null
                : methodSymbol.GetAttributes()
                    .FirstOrDefault(attributeData => AttributeMatchesHttpMethod(attributeData, httpMethod));

            if (httpAttributeData != null)
            {
                requireAuthorization = GetBoolNamedArgument(
                    httpAttributeData,
                    "RequireAuthorization",
                    requireAuthorization);
                authorizationPolicy = GetStringNamedArgument(
                    httpAttributeData,
                    "AuthorizationPolicy") ?? authorizationPolicy;
                roles = GetStringArrayNamedArgument(
                    httpAttributeData,
                    "Roles") ?? roles;
            }
        }

        // Collect DI parameters (skip request + CancellationToken)
        var diParams = new List<(string TypeFullName, string Name, bool IsValidator)>();
        var hasCancellationToken = false;
        var requestTypeFullName = ToGlobalQualified(requestType.ToDisplayString());
        var queryParameters = CollectQueryParameters(requestType);
        var routeParameters = CollectRouteParameters(route, queryParameters);
        var constructorBoundProperties = CollectConstructorBoundProperties(requestType, queryParameters);

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
            QueryParameters = queryParameters,
            RouteParameters = routeParameters,
            ConstructorBoundProperties = constructorBoundProperties,
            HasCancellationToken = hasCancellationToken,
            Namespace = containingClass.ContainingNamespace.ToDisplayString(),
            HasBoltHandler = hasBolt,
            HttpMethod = httpMethod,
            Route = route,
            Tags = tags,
            Summary = summary,
            Description = description,
            ExcludeFromOpenApi = excludeFromOpenApi,
            RequireAuthorization = requireAuthorization,
            AuthorizationPolicy = authorizationPolicy,
            Roles = roles,
            RequiredServiceScopes = requiredServiceScopes,
            AllowedServiceCallers = allowedServiceCallers,
            ValidatorTypeFullName = hasValidator ? validatorInterface : null
        };
    }

    private static List<QueryParameterInfo> CollectQueryParameters(ITypeSymbol requestType)
    {
        var parameters = new List<QueryParameterInfo>();
        if (!(requestType is INamedTypeSymbol namedType))
            return parameters;

        foreach (var property in namedType.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.DeclaredAccessibility != Accessibility.Public ||
                property.IsStatic ||
                property.IsIndexer ||
                property.SetMethod is null)
            {
                continue;
            }

            var parameterType = ToGlobalQualified(property.Type.ToDisplayString());
            var assignWhenHasValue = false;

            if (property.Type.IsValueType && !IsNullableValueType(property.Type))
            {
                parameterType += "?";
                assignWhenHasValue = true;
            }

            parameters.Add(new QueryParameterInfo
            {
                PropertyName = property.Name,
                ParameterName = ToParameterName(property.Name),
                ParameterTypeFullName = parameterType,
                AssignWhenHasValue = assignWhenHasValue,
                IsInitOnly = property.SetMethod?.IsInitOnly == true,
                DefaultValueExpression = GetDefaultValueExpression(property)
            });
        }

        return parameters;
    }

    private static List<QueryParameterInfo> CollectRouteParameters(
        string? route,
        IReadOnlyCollection<QueryParameterInfo> queryParameters)
    {
        if (string.IsNullOrWhiteSpace(route) || queryParameters.Count == 0)
            return [];

        var routeText = route!;
        var routeParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var start = -1;
        for (var i = 0; i < routeText.Length; i++)
        {
            if (routeText[i] == '{')
            {
                start = i + 1;
                continue;
            }

            if (routeText[i] != '}' || start < 0)
                continue;

            var token = routeText.Substring(start, i - start);
            var parameterName = token.Split(':')[0].Trim();
            if (!string.IsNullOrWhiteSpace(parameterName))
                routeParameterNames.Add(parameterName);

            start = -1;
        }

        return queryParameters
            .Where(p => routeParameterNames.Contains(p.ParameterName) ||
                        routeParameterNames.Contains(p.PropertyName))
            .ToList();
    }

    private static List<string> CollectConstructorBoundProperties(
        ITypeSymbol requestType,
        IReadOnlyCollection<QueryParameterInfo> queryParameters)
    {
        if (requestType is not INamedTypeSymbol namedType || queryParameters.Count == 0)
            return new List<string>();

        var queryParametersByName = queryParameters.ToDictionary(
            static p => p.PropertyName,
            static p => p.PropertyName,
            StringComparer.OrdinalIgnoreCase);

        foreach (var constructor in namedType.InstanceConstructors
                     .Where(static c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0)
                     .OrderByDescending(static c => c.Parameters.Length))
        {
            var boundProperties = new List<string>();
            var canBindConstructor = true;

            foreach (var parameter in constructor.Parameters)
            {
                if (!queryParametersByName.TryGetValue(parameter.Name, out var propertyName))
                {
                    canBindConstructor = false;
                    break;
                }

                var queryParameter = queryParameters.First(p =>
                    string.Equals(p.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase));
                var parameterType = ToGlobalQualified(parameter.Type.ToDisplayString());
                if (queryParameter.AssignWhenHasValue)
                    parameterType += "?";

                if (!string.Equals(parameterType, queryParameter.ParameterTypeFullName, StringComparison.Ordinal))
                {
                    canBindConstructor = false;
                    break;
                }

                boundProperties.Add(queryParameter.PropertyName);
            }

            if (canBindConstructor)
                return boundProperties;
        }

        return new List<string>();
    }

    private static string? GetDefaultValueExpression(IPropertySymbol property)
    {
        foreach (var syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is PropertyDeclarationSyntax { Initializer: { } initializer })
                return initializer.Value.NormalizeWhitespace().ToFullString();
        }

        return null;
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static string ToParameterName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
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

    private static bool AttributeMatchesHttpMethod(AttributeData attributeData, string httpMethod)
    {
        var attributeName = attributeData.AttributeClass?.Name;
        return attributeName == httpMethod || attributeName == $"{httpMethod}Attribute";
    }

    private static bool GetBoolNamedArgument(
        AttributeData attributeData,
        string propertyName,
        bool defaultValue)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Value is bool value)
                return value;
        }

        return defaultValue;
    }

    private static string? GetStringNamedArgument(AttributeData attributeData, string propertyName)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Value is string value)
                return value;
        }

        return null;
    }

    private static string[]? GetStringArrayNamedArgument(AttributeData attributeData, string propertyName)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key != propertyName || namedArg.Value.IsNull)
                continue;

            return namedArg.Value.Values
                .Select(static value => value.Value as string)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .ToArray();
        }

        return null;
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

    #region Bolt Handler Generation

    private string GenerateBoltHandler(HandlerInfo h)
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
            callArgs.Append(", ct");

        var requiredServiceScopes = ToStringArrayExpression(h.RequiredServiceScopes);
        var allowedServiceCallers = ToStringArrayExpression(h.AllowedServiceCallers);

        var validationBlock = "";
        if (h.ValidatorTypeFullName != null)
        {
            validationBlock = $@"
                    var validator = scope.ServiceProvider.GetRequiredService<{h.ValidatorTypeFullName}>();
                    var validationResult = await validator.ValidateAsync(request, ct);
                    if (!validationResult.IsValid)
                    {{
                        var errors = validationResult.Errors
                            .GroupBy(static e => e.PropertyName)
                            .ToDictionary(static g => g.Key, static g => g.Select(static e => e.ErrorMessage).ToArray());

                        var validationResponse = new {h.SfResponseTypeFullName}();
                        validationResponse.HttpStatusCode = System.Net.HttpStatusCode.BadRequest;
                        validationResponse.Message = ""Validation failed"";
                        validationResponse.ValidationErrors = errors;
                        var validationResponseBytes = MemoryPackSerializer.Serialize(validationResponse);
                        return (System.Net.HttpStatusCode.BadRequest, (ReadOnlyMemory<byte>)validationResponseBytes);
                    }}
";
        }

        var isQueryResponse = h.SfResponseTypeFullName?.Contains("QueryResponse") == true;
        var isTypedCommandResponse = h.SfResponseTypeFullName?.Contains("CmdResponse<") == true;
        string resultBuild;
        if ((isQueryResponse || isTypedCommandResponse) && h.IsGenericResult && h.ResultDataTypeFullName != null)
        {
            resultBuild = $@"                    var sfResponse = new {h.SfResponseTypeFullName}();
                    sfResponse.HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode;
                    if (result.IsSuccess)
                        sfResponse.Response = result.Data;
                    else
                        sfResponse.Message = result.Message;";
        }
        else
        {
            resultBuild = $@"                    var sfResponse = new {h.SfResponseTypeFullName}();
                    sfResponse.HttpStatusCode = (System.Net.HttpStatusCode)result.StatusCode;
                    sfResponse.Message = result.Message;";
        }

        return $@"// <auto-generated/>
#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Threading;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;

namespace {h.Namespace}.Generated;

public sealed class {h.ClassName}_{h.MethodName}_BoltHandler : IBoltHandler
{{
    public void Register(BoltClient client, ILogger logger, IServiceScopeFactory scopeFactory)
    {{
        logger.LogInformation(""Registering Bolt handler for {{RequestType}} -> {{Endpoint}}"",
            ""{h.RequestTypeName}"", ""{h.ClassFullName}.{h.MethodName}"");

        client.RegisterHandler(""{h.RequestTypeName}"",
            async (ReadOnlyMemory<byte> payload, BoltInboundRequestContext context, CancellationToken ct) =>
            {{
                try
                {{
                    using var scope = scopeFactory.CreateScope();
                    var request = MemoryPackSerializer.Deserialize<{h.RequestTypeFullName}>(payload.Span);
                    if (request is null)
                        return ((System.Net.HttpStatusCode)400, ReadOnlyMemory<byte>.Empty);

                    var authorization = await scope.ServiceProvider
                        .GetRequiredService<IBoltServiceInvocationAuthorizer>()
                        .AuthorizeAsync(
                            request.Metadata,
                            context,
                            requiredScopes: {requiredServiceScopes},
                            allowedCallers: {allowedServiceCallers},
                            ct: ct);
                    if (!authorization.IsSuccess)
                        return ((System.Net.HttpStatusCode)authorization.StatusCode, ReadOnlyMemory<byte>.Empty);

{validationBlock}
{diResolves}
                    var result = await {h.ClassFullName}.{h.MethodName}({callArgs});

{resultBuild}

                    var responseBytes = MemoryPackSerializer.Serialize(sfResponse);
                    return ((System.Net.HttpStatusCode)result.StatusCode, (ReadOnlyMemory<byte>)responseBytes);
                }}
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {{
                    throw;
                }}
                catch (Exception ex)
                {{
                    logger.LogError(ex, ""Bolt handler error for {{RequestType}}"", ""{h.RequestTypeName}"");
                    return (System.Net.HttpStatusCode.InternalServerError, ReadOnlyMemory<byte>.Empty);
                }}
            }});
    }}
}}";
    }

    private static string GenerateQueryRequestInitialization(HandlerInfo h)
    {
        var builder = new StringBuilder();
        var constructorBoundProperties = new HashSet<string>(
            h.ConstructorBoundProperties,
            StringComparer.OrdinalIgnoreCase);
        var requiresInitializer = h.QueryParameters.Any(static p => p.IsInitOnly) ||
                                  constructorBoundProperties.Count > 0;

        if (requiresInitializer)
        {
            var constructorArguments = h.ConstructorBoundProperties
                .Select(propertyName => h.QueryParameters.First(p =>
                    string.Equals(p.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase)))
                .Select(ToQueryValueExpression)
                .ToList();
            var initializerParameters = h.QueryParameters
                .Where(p => !constructorBoundProperties.Contains(p.PropertyName))
                .ToList();
            var constructorCall = constructorArguments.Count == 0
                ? "()"
                : $"({string.Join(", ", constructorArguments)})";

            if (initializerParameters.Count == 0)
            {
                builder.AppendLine($"        var request = new {h.RequestTypeFullName}{constructorCall};");
            }
            else
            {
                builder.AppendLine($"        var request = new {h.RequestTypeFullName}{constructorCall}");
                builder.AppendLine("        {");
                foreach (var queryParameter in initializerParameters)
                {
                    builder.AppendLine(
                        $"            {queryParameter.PropertyName} = {ToQueryValueExpression(queryParameter)},");
                }
                builder.AppendLine("        };");
            }

            return builder.ToString();
        }

        builder.AppendLine($"        var request = new {h.RequestTypeFullName}();");

        foreach (var queryParameter in h.QueryParameters)
        {
            if (queryParameter.AssignWhenHasValue)
            {
                builder.AppendLine(
                    $"        if ({queryParameter.ParameterName}.HasValue) request.{queryParameter.PropertyName} = {queryParameter.ParameterName}.Value;");
            }
            else
            {
                builder.AppendLine(
                    $"        request.{queryParameter.PropertyName} = {queryParameter.ParameterName};");
            }
        }

        return builder.ToString();
    }

    private static string GenerateBodyRouteAssignments(HandlerInfo h)
    {
        if (h.RouteParameters.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var routeParameter in h.RouteParameters)
        {
            if (routeParameter.AssignWhenHasValue)
            {
                builder.AppendLine(
                    $"        if ({routeParameter.ParameterName}.HasValue) request.{routeParameter.PropertyName} = {routeParameter.ParameterName}.Value;");
            }
            else
            {
                builder.AppendLine(
                    $"        request.{routeParameter.PropertyName} = {routeParameter.ParameterName};");
            }
        }

        return builder.ToString();
    }

    private static string ToQueryValueExpression(QueryParameterInfo queryParameter)
    {
        if (!queryParameter.AssignWhenHasValue)
            return queryParameter.ParameterName;

        return queryParameter.DefaultValueExpression != null
            ? $"{queryParameter.ParameterName}.GetValueOrDefault({queryParameter.DefaultValueExpression})"
            : $"{queryParameter.ParameterName}.GetValueOrDefault()";
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

        // Build the endpoint chain (.WithTags, .WithSummary, .WithDescription, .ExcludeFromDescription)
        var chain = new StringBuilder();
        if (h.Tags?.Length > 0)
            chain.AppendLine($"            .WithTags({string.Join(", ", h.Tags.Select(ToCSharpStringLiteral))})");
        if (h.Summary != null)
            chain.AppendLine($"            .WithSummary({ToCSharpStringLiteral(h.Summary)})");
        if (h.Description != null)
            chain.AppendLine($"            .WithDescription({ToCSharpStringLiteral(h.Description)})");
        chain.Append(GenerateAuthorizationConfiguration(h));
        if (h.ExcludeFromOpenApi)
            chain.AppendLine("            .ExcludeFromDescription()");

        // Build REST handler parameters
        var restParams = new StringBuilder();
        var callArgs = new StringBuilder("request");
        var requestInitialization = "";

        // GET/DELETE bind explicit query parameters to avoid inherited complex properties.
        void AppendRestParameter(string parameter)
        {
            if (restParams.Length > 0)
                restParams.Append(", ");
            restParams.Append(parameter);
        }

        var isBodylessMethod = httpMapMethod is "MapGet" or "MapDelete";
        if (isBodylessMethod)
        {
            foreach (var queryParameter in h.QueryParameters)
                AppendRestParameter($"{queryParameter.ParameterTypeFullName} {queryParameter.ParameterName}");

            requestInitialization = GenerateQueryRequestInitialization(h);
        }
        else
        {
            AppendRestParameter($"{h.RequestTypeFullName} request");
            foreach (var routeParameter in h.RouteParameters)
                AppendRestParameter($"{routeParameter.ParameterTypeFullName} {routeParameter.ParameterName}");

            requestInitialization = GenerateBodyRouteAssignments(h);
        }

        // DI service parameters
        foreach (var (typeFullName, name, _) in h.DiParameters)
        {
            AppendRestParameter($"{typeFullName} @{name}");
            callArgs.Append($", @{name}");
        }

        // Validator parameter (auto-detected)
        var hasValidator = h.ValidatorTypeFullName != null;
        if (hasValidator)
            AppendRestParameter($"{h.ValidatorTypeFullName} validator");

        // CancellationToken
        AppendRestParameter("CancellationToken ct");
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
    {{
{requestInitialization}{validationBlock}
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
            $"        new {h.ClassName}_{h.MethodName}_BoltHandler(),"));

        return $@"// <auto-generated/>
#nullable enable
using System;
using System.Collections.Generic;
using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;
{usings}

namespace {assemblyName}.Generated;

public static class BoltHandlerRegistry
{{
    public static IReadOnlyList<IBoltHandler> Handlers {{ get; }} = new IBoltHandler[]
    {{
{entries}
    }};

    /// <summary>
    /// Register all generated Bolt handlers on the given BoltClient.
    /// Call from your app startup after BoltClient is created.
    /// </summary>
    public static void RegisterAll(BoltClient client, ILogger logger, IServiceScopeFactory scopeFactory)
    {{
        foreach (var handler in Handlers)
        {{
            handler.Register(client, logger, scopeFactory);
        }}
    }}
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

    private static string GenerateAuthorizationConfiguration(HandlerInfo h)
    {
        var policy = string.IsNullOrWhiteSpace(h.AuthorizationPolicy) ? null : h.AuthorizationPolicy;
        var roles = h.Roles?
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .ToArray();
        var hasRoles = roles is { Length: > 0 };

        if (!h.RequireAuthorization && policy == null && !hasRoles)
            return string.Empty;

        var builder = new StringBuilder();

        if (policy != null)
            builder.AppendLine($"            .RequireAuthorization({ToCSharpStringLiteral(policy)})");

        if (hasRoles)
        {
            var roleArguments = string.Join(", ", roles!.Select(ToCSharpStringLiteral));
            builder.AppendLine($"            .RequireAuthorization(policy => policy.RequireRole({roleArguments}))");
        }

        if (policy == null && !hasRoles)
            builder.AppendLine("            .RequireAuthorization()");

        return builder.ToString();
    }

    private static string ToCSharpStringLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                '\0' => @"\0",
                '\a' => @"\a",
                '\b' => @"\b",
                '\f' => @"\f",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                '\v' => @"\v",
                _ => c.ToString()
            });
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string ToStringArrayExpression(IReadOnlyCollection<string>? values) =>
        values is not { Count: > 0 }
            ? "null"
            : $"new string[] {{ {string.Join(", ", values.Select(ToCSharpStringLiteral))} }}";

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
        public List<QueryParameterInfo> QueryParameters { get; set; } = new();
        public List<QueryParameterInfo> RouteParameters { get; set; } = new();
        public List<string> ConstructorBoundProperties { get; set; } = new();
        public bool HasCancellationToken { get; set; }
        public string Namespace { get; set; } = "";

        // Bolt
        public bool HasBoltHandler { get; set; }

        // REST
        public string? HttpMethod { get; set; }
        public string? Route { get; set; }
        public string[]? Tags { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public bool ExcludeFromOpenApi { get; set; }
        public bool RequireAuthorization { get; set; }
        public string? AuthorizationPolicy { get; set; }
        public string[]? Roles { get; set; }

        // Bolt service authorization
        public string[]? RequiredServiceScopes { get; set; }
        public string[]? AllowedServiceCallers { get; set; }

        // Validation
        public string? ValidatorTypeFullName { get; set; }
    }

    private class QueryParameterInfo
    {
        public string PropertyName { get; set; } = "";
        public string ParameterName { get; set; } = "";
        public string ParameterTypeFullName { get; set; } = "";
        public bool AssignWhenHasValue { get; set; }
        public bool IsInitOnly { get; set; }
        public string? DefaultValueExpression { get; set; }
    }

    #endregion
}
