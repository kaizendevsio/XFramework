using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that creates minimal API endpoints for entities
/// marked with the GenerateEndpointsAttribute where Type is Rest or Both.
/// </summary>
[Generator]
public class EntityEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register syntax provider to find classes with GenerateEndpoints attribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Register source output
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        // Check if it's a class with attributes
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static EndpointInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Get the symbol for the class
        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return null;

        // Look for GenerateEndpointsAttribute
        foreach (var attributeData in classSymbol.GetAttributes())
        {
            var attributeClass = attributeData.AttributeClass;
            if (attributeClass == null)
                continue;
                
            // Check both short name and full name to be more robust
            if (attributeClass.Name != "GenerateEndpointsAttribute" &&
                attributeClass.ToDisplayString() != "XFramework.Core.Attributes.GenerateEndpointsAttribute")
                continue;

            // Extract attribute properties
            var type = GetEnumValue<int>(attributeData, "Type", 3); // Default Both = 3
            var actions = GetEnumValue<int>(attributeData, "Actions", 31); // Default All = 31
            var routePrefix = GetStringValue(attributeData, "RoutePrefix");
            var requireAuth = GetBoolValue(attributeData, "RequireAuthorization", true);
            var cacheDuration = GetIntValue(attributeData, "CacheDurationSeconds", 300);
            var roles = GetStringArrayValue(attributeData, "Roles");

            // Only generate endpoints if Type is Rest (2) or Both (3)
            if (type != 2 && type != 3)
                return null;

            // Default route prefix to entity name in lowercase with plural
            var defaultRoute = $"api/{ToPlural(classSymbol.Name.ToLowerInvariant())}";

            return new EndpointInfo
            {
                ClassName = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                RoutePrefix = routePrefix ?? defaultRoute,
                Actions = actions,
                RequireAuthorization = requireAuth,
                Roles = roles,
                CacheDurationSeconds = cacheDuration
            };
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<EndpointInfo> entities, SourceProductionContext context)
    {
        if (entities.IsDefaultOrEmpty)
            return;

        foreach (var entity in entities)
        {
            if (entity == null)
                continue;

            // Generate the endpoint class
            var source = GenerateEndpointSource(entity);
            context.AddSource($"{entity.ClassName}Endpoints.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateEndpointSource(EndpointInfo entity)
    {
        var entityName = entity.ClassName;
        var entityLower = entityName.ToLowerInvariant();
        var entityPlural = ToPlural(entityName);
        var methods = new StringBuilder();

        // Generate GET by ID endpoint (Actions & 2)
        if ((entity.Actions & 2) != 0)
        {
            methods.AppendLine(GenerateGetByIdEndpoint(entity, entityName, entityLower));
        }

        // Generate GET list endpoint (Actions & 4)
        if ((entity.Actions & 4) != 0)
        {
            methods.AppendLine(GenerateGetListEndpoint(entity, entityName, entityLower));
        }

        // Generate POST create endpoint (Actions & 1)
        if ((entity.Actions & 1) != 0)
        {
            methods.AppendLine(GenerateCreateEndpoint(entity, entityName));
        }

        // Generate PUT update endpoint (Actions & 8)
        if ((entity.Actions & 8) != 0)
        {
            methods.AppendLine(GenerateUpdateEndpoint(entity, entityName));
        }

        // Generate DELETE endpoint (Actions & 16)
        if ((entity.Actions & 16) != 0)
        {
            methods.AppendLine(GenerateDeleteEndpoint(entity, entityName));
        }

        return $$"""
            // <auto-generated/>
            #nullable enable
            #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Routing;
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            namespace {{entity.Namespace}}
            {
                /// <summary>
                /// Auto-generated endpoints for {{entityName}} entity.
                /// Generated: {{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}} UTC
                /// </summary>
                public static class {{entityName}}Endpoints
                {
                    /// <summary>
                    /// Maps all {{entityName}} endpoints to the application.
                    /// </summary>
                    public static IEndpointRouteBuilder Map{{entityName}}Endpoints(this IEndpointRouteBuilder app)
                    {
                        var group = app.MapGroup("{{entity.RoutePrefix}}")
                            .WithTags("{{entityPlural}}")
                            .ExcludeFromDescription();
                        
            {{methods}}
                        return app;
                    }
                }
            }

            #pragma warning restore CS1591
            """;
    }

    private static string GenerateGetByIdEndpoint(EndpointInfo entity, string entityName, string entityLower)
    {
        var authConfig = GenerateAuthConfiguration(entity);
        var cacheComment = entity.CacheDurationSeconds > 0
            ? $" // TODO: Apply OutputCaching policy '{entityLower}-cache' ({entity.CacheDurationSeconds}s)"
            : "";

        return $$"""
                    // GET by ID{{cacheComment}}
                    group.MapGet("/{id:guid}", async (
                        Guid id,
                        I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
                        var result = await service.GetByIdAsync(id, includeNavigations: false, navigationDepth: 1, ct: ct);
                        
                        return result.IsSuccess
                            ? Results.Ok(result.Data)
                            : result.StatusCode switch
                            {
                                404 => Results.NotFound(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Get{{entityName}}")
                    .WithSummary("Get {{entityName}} by ID"){{authConfig}}
                    .Produces<{{entityName}}>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status404NotFound);
                    
            """;
    }

    private static string GenerateGetListEndpoint(EndpointInfo entity, string entityName, string entityLower)
    {
        var authConfig = GenerateAuthConfiguration(entity);
        var cacheComment = entity.CacheDurationSeconds > 0
            ? $" // TODO: Apply OutputCaching policy '{entityLower}-list-cache' ({entity.CacheDurationSeconds}s)"
            : "";

        return $$"""
                    // GET list{{cacheComment}}
                    group.MapGet("/", async (
                        [AsParameters] Get{{entityName}}ListRequest request,
                        I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
                        var result = await service.GetListAsync(request, includeNavigations: false, navigationDepth: 1, ct: ct);
                        
                        return result.IsSuccess
                            ? Results.Ok(result.Data)
                            : Results.Problem(result.Message, statusCode: result.StatusCode);
                    })
                    .WithName("Get{{entityName}}List")
                    .WithSummary("Get list of {{entityName}} entities"){{authConfig}}
                    .Produces<List<{{entityName}}>>(StatusCodes.Status200OK);
                    
            """;
    }

    private static string GenerateCreateEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity);

        return $$"""
                    // POST create
                    group.MapPost("/", async (
                        Create{{entityName}}Request request,
                        I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
                        var result = await service.CreateAsync(request, ct);
                        
                        return result.IsSuccess
                            ? Results.Created($"{{entity.RoutePrefix}}/{result.Data!.Id}", result.Data)
                            : result.StatusCode switch
                            {
                                400 => Results.BadRequest(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Create{{entityName}}")
                    .WithSummary("Create a new {{entityName}}"){{authConfig}}
                    .ProducesValidationProblem()
                    .Produces<{{entityName}}>(StatusCodes.Status201Created)
                    .Produces(StatusCodes.Status400BadRequest);
                    
            """;
    }

    private static string GenerateUpdateEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity);

        return $$"""
                    // PUT update
                    group.MapPut("/{id:guid}", async (
                        Guid id,
                        Update{{entityName}}Request request,
                        I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
                        var result = await service.UpdateAsync(id, request, ct);
                        
                        return result.IsSuccess
                            ? Results.Ok(result.Data)
                            : result.StatusCode switch
                            {
                                404 => Results.NotFound(result.Message),
                                400 => Results.BadRequest(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Update{{entityName}}")
                    .WithSummary("Update a {{entityName}}"){{authConfig}}
                    .Produces<{{entityName}}>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status404NotFound)
                    .Produces(StatusCodes.Status400BadRequest);
                    
            """;
    }

    private static string GenerateDeleteEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity);

        return $$"""
                    // DELETE
                    group.MapDelete("/{id:guid}", async (
                        Guid id,
                        I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
                        var result = await service.DeleteAsync(id, ct);
                        
                        return result.IsSuccess
                            ? Results.NoContent()
                            : result.StatusCode switch
                            {
                                404 => Results.NotFound(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Delete{{entityName}}")
                    .WithSummary("Delete a {{entityName}}"){{authConfig}}
                    .Produces(StatusCodes.Status204NoContent)
                    .Produces(StatusCodes.Status404NotFound);
            """;
    }

    private static string GenerateAuthConfiguration(EndpointInfo entity)
    {
        if (!entity.RequireAuthorization)
            return "";

        if (entity.Roles != null && entity.Roles.Length > 0)
        {
            var rolesString = string.Join("\", \"", entity.Roles);
            return $"\n            .RequireAuthorization(policy => policy.RequireRole(\"{rolesString}\"))";
        }

        return "\n            .RequireAuthorization()";
    }

    private static string ToPlural(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        // Simple pluralization rules
        if (word.EndsWith("y") && word.Length > 1 && !"aeiou".Contains(word[word.Length - 2]))
            return word.Substring(0, word.Length - 1) + "ies";
        
        if (word.EndsWith("s") || word.EndsWith("x") || word.EndsWith("z") || 
            word.EndsWith("ch") || word.EndsWith("sh"))
            return word + "es";
        
        return word + "s";
    }

    private static T GetEnumValue<T>(AttributeData attributeData, string propertyName, T defaultValue)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Value != null)
            {
                return (T)namedArg.Value.Value;
            }
        }
        return defaultValue;
    }

    private static int GetIntValue(AttributeData attributeData, string propertyName, int defaultValue)
    {
        return GetEnumValue<int>(attributeData, propertyName, defaultValue);
    }

    private static bool GetBoolValue(AttributeData attributeData, string propertyName, bool defaultValue)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Value != null)
            {
                return (bool)namedArg.Value.Value;
            }
        }
        return defaultValue;
    }

    private static string? GetStringValue(AttributeData attributeData, string propertyName)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Value is string value)
            {
                return value;
            }
        }
        return null;
    }

    private static string[]? GetStringArrayValue(AttributeData attributeData, string propertyName)
    {
        foreach (var namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == propertyName && !namedArg.Value.IsNull)
            {
                var values = namedArg.Value.Values;
                return values.Select(v => v.Value?.ToString()).Where(v => v != null).ToArray()!;
            }
        }
        return null;
    }
}

internal class EndpointInfo
{
    public string ClassName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string RoutePrefix { get; set; } = string.Empty;
    public int Actions { get; set; }
    public bool RequireAuthorization { get; set; }
    public string[]? Roles { get; set; }
    public int CacheDurationSeconds { get; set; }
}