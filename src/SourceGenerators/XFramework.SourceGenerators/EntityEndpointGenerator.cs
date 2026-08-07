using System.Collections.Immutable;
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
                ActorRequirement = GetEnumValue(attributeData, "ActorRequirement", 0),
                TenantAccessMode = GetEnumValue(attributeData, "TenantAccessMode", 0),
                CrossTenantCapability = GetStringValue(attributeData, "CrossTenantCapability")
                    ?? "identity.tenants:manage",
                AuthorizationFeature = GetStringValue(attributeData, "AuthorizationFeature"),
                ReadCapability = GetStringValue(attributeData, "ReadCapability") ?? "view",
                CreateCapability = GetStringValue(attributeData, "CreateCapability") ?? "create",
                UpdateCapability = GetStringValue(attributeData, "UpdateCapability") ?? "update",
                DeleteCapability = GetStringValue(attributeData, "DeleteCapability") ?? "delete",
                ActorAttributes = GetActorAttributes(classSymbol),
                IsBaseModel = InheritsBaseModel(classSymbol)
            };
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<EndpointInfo> entities, SourceProductionContext context)
    {
        var allEntities = new List<EndpointInfo>();

        if (!entities.IsDefaultOrEmpty)
        {
            allEntities.AddRange(entities.Where(e => e != null));
        }

        // Discover entities from referenced assemblies
        var referencedEntities = DiscoverFromReferencedAssemblies(compilation);
        allEntities.AddRange(referencedEntities);

        var seen = new HashSet<string>();
        var generatedEntities = new List<EndpointInfo>();
        foreach (var entity in allEntities)
        {
            if (!seen.Add(entity.ClassName))
                continue;

            entity.HasCreateValidator = HasValidator(
                compilation,
                $"{entity.Namespace}.Create{entity.ClassName}Request");
            entity.HasUpdateValidator = HasValidator(
                compilation,
                $"{entity.Namespace}.Update{entity.ClassName}Request");

            var source = GenerateEndpointSource(entity);
            context.AddSource($"{entity.ClassName}Endpoints.g.cs", SourceText.From(source, Encoding.UTF8));
            generatedEntities.Add(entity);
        }

        if (generatedEntities.Count > 0)
        {
            context.AddSource(
                "GeneratedEntityEndpointRoutes.g.cs",
                SourceText.From(GenerateEndpointRegistry(generatedEntities), Encoding.UTF8));
        }
    }

    private static string GenerateEndpointRegistry(IReadOnlyCollection<EndpointInfo> entities)
    {
        var registrations = string.Join(
            Environment.NewLine,
            entities.Select(entity =>
                $"        global::{entity.Namespace}.{entity.ClassName}Endpoints.Map{entity.ClassName}Endpoints(app);"));

        return $$"""
                 // <auto-generated/>
                 #nullable enable
                 using Microsoft.AspNetCore.Routing;

                 namespace XFramework.GeneratedEndpoints;

                 public static class GeneratedEntityEndpointRoutes
                 {
                     public static IEndpointRouteBuilder MapGeneratedEntityEndpoints(this IEndpointRouteBuilder app)
                     {
                 {{registrations}}
                         return app;
                     }
                 }
                 """;
    }

    private static bool HasValidator(Compilation compilation, string requestMetadataName)
    {
        var requestType = compilation.GetTypeByMetadataName(requestMetadataName);
        var validatorType = compilation.GetTypeByMetadataName("FluentValidation.IValidator`1");
        if (requestType is null || validatorType is null)
            return false;

        return GetAllTypes(compilation.Assembly.GlobalNamespace)
            .Where(static type => !type.IsAbstract)
            .SelectMany(static type => type.AllInterfaces)
            .Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, validatorType) &&
                candidate.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], requestType));
    }

    private static List<EndpointInfo> DiscoverFromReferencedAssemblies(Compilation compilation)
    {
        var results = new List<EndpointInfo>();
        var coreAttr = "XFramework.Core.Attributes.GenerateEndpointsAttribute";
        var sharedAttr = "XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute";

        // Only discover from referenced assemblies in Api projects
        var assemblyName = compilation.AssemblyName ?? "";
        if (!assemblyName.EndsWith(".Api"))
            return results;
        var modulePrefix = assemblyName.Contains('.') ? assemblyName.Split('.')[0] : assemblyName;

        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is not IAssemblySymbol assembly)
                continue;

            // Only discover from this module's own Domain.Shared assembly
            if (!assembly.Name.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var type in GetAllTypes(assembly.GlobalNamespace))
            {
                if (type.IsAbstract || type.TypeKind != TypeKind.Class)
                    continue;

                foreach (var attr in type.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != coreAttr && attr.AttributeClass?.ToDisplayString() != sharedAttr)
                        continue;

                    var attrType = GetEnumValue<int>(attr, "Type", 3);
                    var actions = GetEnumValue<int>(attr, "Actions", 31);
                    var routePrefix = GetStringValue(attr, "RoutePrefix");
                    var requireAuth = GetBoolValue(attr, "RequireAuthorization", true);
                    var roles = GetStringArrayValue(attr, "Roles");

                    if (attrType != 2 && attrType != 3) // Rest or Both
                        continue;

                    var defaultRoute = $"api/{ToPlural(type.Name.ToLowerInvariant())}";

                    results.Add(new EndpointInfo
                    {
                        ClassName = type.Name,
                        Namespace = type.ContainingNamespace.ToDisplayString(),
                        RoutePrefix = routePrefix ?? defaultRoute,
                        Actions = actions,
                        RequireAuthorization = requireAuth,
                        Roles = roles,
                        ActorRequirement = GetEnumValue(attr, "ActorRequirement", 0),
                        TenantAccessMode = GetEnumValue(attr, "TenantAccessMode", 0),
                        CrossTenantCapability = GetStringValue(attr, "CrossTenantCapability")
                            ?? "identity.tenants:manage",
                        AuthorizationFeature = GetStringValue(attr, "AuthorizationFeature"),
                        ReadCapability = GetStringValue(attr, "ReadCapability") ?? "view",
                        CreateCapability = GetStringValue(attr, "CreateCapability") ?? "create",
                        UpdateCapability = GetStringValue(attr, "UpdateCapability") ?? "update",
                        DeleteCapability = GetStringValue(attr, "DeleteCapability") ?? "delete",
                        ActorAttributes = GetActorAttributes(type),
                        IsBaseModel = InheritsBaseModel(type)
                    });

                    break;
                }
            }
        }

        return results;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNs))
                yield return type;
        }
    }

    private static string GenerateEndpointSource(EndpointInfo entity)
    {
        var entityName = entity.ClassName;
        var entityPlural = ToPlural(entityName);
        var methods = new StringBuilder();

        // Generate GET by ID endpoint (Actions & 2)
        if ((entity.Actions & 2) != 0)
        {
            methods.AppendLine(GenerateGetByIdEndpoint(entity, entityName));
        }

        // Generate GET list endpoint (Actions & 4)
        if ((entity.Actions & 4) != 0)
        {
            methods.AppendLine(GenerateGetListEndpoint(entity, entityName));
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
            using System.Linq;
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
                            .WithTags("{{entityPlural}}");
                        
            {{methods}}
                        return app;
                    }
                }
            }

            #pragma warning restore CS1591
            """;
    }

    private static string GenerateGetByIdEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity);
        var trustedInvocationParameters = GenerateTrustedInvocationParameters(entity);
        var trustedInvocationAuthorization = GenerateTrustedInvocationAuthorization(
            entity,
            "GET",
            $"{entity.RoutePrefix}/{{id:guid}}",
            "view");

        return $$"""
                    // GET by ID
                    group.MapGet("/{id:guid}", async (
                        Guid id,
                        {{trustedInvocationParameters}}[FromServices] I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
            {{trustedInvocationAuthorization}}
                        var result = await service.GetByIdAsync(id, includeNavigations: false, navigationDepth: 1, ct: ct);
                        
                        return result.IsSuccess
                            ? Results.Ok(result.Data)
                            : result.StatusCode switch
                            {
                                404 => Results.NotFound(result.Message),
                                401 => Results.Unauthorized(),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Get{{entityName}}")
                    .WithSummary("Get {{entityName}} by ID"){{authConfig}}
                    .Produces<Generated{{entityName}}Response>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status404NotFound)
                    .Produces(StatusCodes.Status401Unauthorized);
                    
            """;
    }

    private static string GenerateGetListEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity);
        var trustedInvocationParameters = GenerateTrustedInvocationParameters(entity);
        var trustedInvocationAuthorization = GenerateTrustedInvocationAuthorization(
            entity,
            "GET",
            $"{entity.RoutePrefix}/",
            "view");

        return $$"""
                    // GET list
                    group.MapGet("/", async (
                        [AsParameters] Get{{entityName}}ListRequest request,
                        {{trustedInvocationParameters}}[FromServices] I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
            {{trustedInvocationAuthorization}}
                        var result = await service.GetListAsync(request, includeNavigations: false, navigationDepth: 1, ct: ct);
                        
                        return result.IsSuccess
                            ? Results.Ok(result.Data)
                            : result.StatusCode switch
                            {
                                400 => Results.BadRequest(result.Message),
                                401 => Results.Unauthorized(),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Get{{entityName}}List")
                    .WithSummary("Get list of {{entityName}} entities"){{authConfig}}
                    .Produces<List<Generated{{entityName}}Response>>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status401Unauthorized);
                    
            """;
    }

    private static string GenerateCreateEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity, "Create");
        var trustedInvocationParameters = GenerateTrustedInvocationParameters(entity);
        var trustedInvocationAuthorization = GenerateTrustedInvocationAuthorization(
            entity,
            "POST",
            $"{entity.RoutePrefix}/",
            "create");
        var validatorParameter = entity.HasCreateValidator
            ? $"global::FluentValidation.IValidator<global::{entity.Namespace}.Create{entityName}Request> validator,\n                        "
            : string.Empty;
        var validation = entity.HasCreateValidator
            ? GenerateValidationBlock()
            : string.Empty;

        return $$"""
                    // POST create
                    group.MapPost("/", async (
                        Create{{entityName}}Request request,
                        {{validatorParameter}}{{trustedInvocationParameters}}[FromServices] I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
            {{trustedInvocationAuthorization}}
            {{validation}}
                        var result = await service.CreateAsync(request, ct);
                        
                        return result.IsSuccess
                            ? Results.Created(
                                $"{{entity.RoutePrefix}}/{result.Data!.Id}",
                                Generated{{entityName}}Response.FromEntity(result.Data))
                            : result.StatusCode switch
                            {
                                400 => Results.BadRequest(result.Message),
                                401 => Results.Unauthorized(),
                                409 => Results.Conflict(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Create{{entityName}}")
                    .WithSummary("Create a new {{entityName}}"){{authConfig}}
                    .ProducesValidationProblem()
                    .Produces<Generated{{entityName}}Response>(StatusCodes.Status201Created)
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status401Unauthorized)
                    .Produces(StatusCodes.Status409Conflict);
                    
            """;
    }

    private static string GenerateUpdateEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity, "Update");
        var trustedInvocationParameters = GenerateTrustedInvocationParameters(entity);
        var trustedInvocationAuthorization = GenerateTrustedInvocationAuthorization(
            entity,
            "PUT",
            $"{entity.RoutePrefix}/{{id:guid}}",
            "update");
        var validatorParameter = entity.HasUpdateValidator
            ? $"global::FluentValidation.IValidator<global::{entity.Namespace}.Update{entityName}Request> validator,\n                        "
            : string.Empty;
        var validation = entity.HasUpdateValidator
            ? GenerateValidationBlock()
            : string.Empty;
        var validationResponse = entity.HasUpdateValidator
            ? "\n                    .ProducesValidationProblem()"
            : string.Empty;
        var concurrencyParameter = entity.IsBaseModel
            ? "Guid expectedConcurrencyStamp,\n                        "
            : string.Empty;
        var concurrencyArgument = entity.IsBaseModel
            ? "expectedConcurrencyStamp, "
            : string.Empty;

        return $$"""
                    // PUT update
                    group.MapPut("/{id:guid}", async (
                        Guid id,
                        {{concurrencyParameter}}Update{{entityName}}Request request,
                        {{validatorParameter}}{{trustedInvocationParameters}}[FromServices] I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
            {{trustedInvocationAuthorization}}
            {{validation}}
                        var result = await service.UpdateAsync(id, {{concurrencyArgument}}request, ct);
                        
                        return result.IsSuccess
                            ? Results.Ok(Generated{{entityName}}Response.FromEntity(result.Data!))
                            : result.StatusCode switch
                            {
                                404 => Results.NotFound(result.Message),
                                400 => Results.BadRequest(result.Message),
                                401 => Results.Unauthorized(),
                                409 => Results.Conflict(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Update{{entityName}}")
                    .WithSummary("Update a {{entityName}}"){{authConfig}}{{validationResponse}}
                    .Produces<Generated{{entityName}}Response>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status404NotFound)
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status401Unauthorized)
                    .Produces(StatusCodes.Status409Conflict);
                    
            """;
    }

    private static bool InheritsBaseModel(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Name == "BaseModel" &&
                current.ContainingNamespace.ToDisplayString() == "XFramework.Domain.Shared.Contracts.Base")
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateValidationBlock() =>
        """
                        var validationResult = await validator.ValidateAsync(request, ct);
                        if (!validationResult.IsValid)
                        {
                            var errors = validationResult.Errors
                                .GroupBy(static error => error.PropertyName)
                                .ToDictionary(
                                    static group => group.Key,
                                    static group => group.Select(static error => error.ErrorMessage).ToArray());
                            return Results.ValidationProblem(errors);
                        }

        """;

    private static string GenerateDeleteEndpoint(EndpointInfo entity, string entityName)
    {
        var authConfig = GenerateAuthConfiguration(entity, "Delete");
        var trustedInvocationParameters = GenerateTrustedInvocationParameters(entity);
        var trustedInvocationAuthorization = GenerateTrustedInvocationAuthorization(
            entity,
            "DELETE",
            $"{entity.RoutePrefix}/{{id:guid}}",
            "delete");
        var concurrencyParameter = entity.IsBaseModel
            ? "Guid expectedConcurrencyStamp,\n                        "
            : string.Empty;
        var concurrencyArgument = entity.IsBaseModel
            ? "expectedConcurrencyStamp, "
            : string.Empty;

        return $$"""
                    // DELETE
                    group.MapDelete("/{id:guid}", async (
                        Guid id,
                        {{concurrencyParameter}}{{trustedInvocationParameters}}[FromServices] I{{entityName}}Service service,
                        CancellationToken ct) =>
                    {
            {{trustedInvocationAuthorization}}
                        var result = await service.DeleteAsync(id, {{concurrencyArgument}}ct);
                        
                        return result.IsSuccess
                            ? Results.NoContent()
                            : result.StatusCode switch
                            {
                                404 => Results.NotFound(result.Message),
                                400 => Results.BadRequest(result.Message),
                                401 => Results.Unauthorized(),
                                409 => Results.Conflict(result.Message),
                                _ => Results.Problem(result.Message, statusCode: result.StatusCode)
                            };
                    })
                    .WithName("Delete{{entityName}}")
                    .WithSummary("Delete a {{entityName}}"){{authConfig}}
                    .Produces(StatusCodes.Status204NoContent)
                    .Produces(StatusCodes.Status404NotFound)
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status401Unauthorized)
                    .Produces(StatusCodes.Status409Conflict);
            """;
    }

    private static string GenerateTrustedInvocationParameters(EndpointInfo entity) =>
        entity.RequireAuthorization
            ? """
              HttpContext httpContext,
                                      [FromServices] global::XFramework.Integration.Security.IHttpTrustedInvocationAuthorizer invocationAuthorizer,
                                      [FromServices] global::XFramework.Integration.Security.IActorAccessTokenScope actorAccessTokenScope,
                                      [FromServices] global::XFramework.Core.Services.FeatureGates.ITrustedInvocationFeatureGate trustedInvocationFeatureGate,

              """
            : string.Empty;

    private static string GenerateTrustedInvocationAuthorization(
        EndpointInfo entity,
        string httpMethod,
        string route,
        string capability) =>
        entity.RequireAuthorization
            ? $$"""
                        var authorizationHeader = httpContext.Request.Headers.Authorization.ToString();
                        var actorAccessToken = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? authorizationHeader[7..].Trim()
                            : null;
                        using var actorTokenLease = string.IsNullOrWhiteSpace(actorAccessToken)
                            ? null
                            : actorAccessTokenScope.Push(actorAccessToken);
                        var invocationAuthorization = await invocationAuthorizer.AuthorizeAsync(
                            authorizationHeader,
                            httpContext.Request.Headers["X-XFramework-Service-Authorization"].ToString(),
                            new global::XFramework.Domain.Shared.BusinessObjects.RequestMetadata(),
                            new global::XFramework.Integration.Security.InvocationAuthorizationPolicy
                            {
                                ActorRequirement = global::XFramework.Integration.Security.ActorRequirement.{{ResolveActorRequirement(entity.ActorRequirement)}},
                                TenantAccessMode = global::XFramework.Integration.Security.TenantAccessMode.{{ResolveTenantAccessMode(entity.TenantAccessMode)}},
                                RequireServiceIdentity = false,
                                RequiredActorRoles = {{StringArrayLiteral(entity.Roles ?? [])}},
                                RequiredActorCapabilities = {{StringArrayLiteral(ResolveRequiredCapabilities(entity, capability))}},
                                RequiredActorAttributes = {{DictionaryLiteral(entity.ActorAttributes, CapabilityActionMask(capability))}},
                                RequiredCrossTenantActorCapabilities = {{StringArrayLiteral(string.IsNullOrWhiteSpace(entity.CrossTenantCapability) ? [] : [entity.CrossTenantCapability])}}
                            },
                            ct);
                        if (!invocationAuthorization.IsSuccess)
                        {
                            return Results.Problem(
                                detail: invocationAuthorization.Error,
                                statusCode: invocationAuthorization.StatusCode);
                        }

                        var featureGateResult = await trustedInvocationFeatureGate.EnsureGeneratedEntityAllowedAsync(
                            "{{Escape(entity.AuthorizationFeature ?? string.Empty)}}",
                            "{{ResolveCapability(entity, capability)}}",
                            {{(ResolveTenantAccessMode(entity.TenantAccessMode) != "Tenantless").ToString().ToLowerInvariant()}},
                            ct);
                        if (!featureGateResult.IsSuccess)
                        {
                            return Results.Problem(
                                detail: featureGateResult.Message,
                                statusCode: featureGateResult.StatusCode);
                        }

              """
            : string.Empty;

    private static string GenerateAuthConfiguration(EndpointInfo entity, string? capability = null)
    {
        if (!entity.RequireAuthorization)
            return "";

        var capabilityMetadata = capability is null
            ? string.Empty
            : $"\n            .WithMetadata(new global::XFramework.Core.Services.FeatureGates.TenantCapabilityRequirement(\"{ResolveCapability(entity, capability)}\"))";

        return $"\n            .RequireAuthorization(){capabilityMetadata}";
    }

    private static List<ActorAttributeInfo> GetActorAttributes(INamedTypeSymbol type)
    {
        var result = new List<ActorAttributeInfo>();
        foreach (var attribute in type.GetAttributes().Where(attribute =>
                     attribute.AttributeClass?.ToDisplayString() ==
                     "XFramework.Domain.Shared.Attributes.RequireGeneratedActorAttributeAttribute"))
        {
            if (attribute.ConstructorArguments.Length != 2 ||
                attribute.ConstructorArguments[0].Value is not string name ||
                attribute.ConstructorArguments[1].Value is not string value)
            {
                continue;
            }

            result.Add(new ActorAttributeInfo(
                name,
                value,
                GetEnumValue(attribute, "Actions", 31)));
        }

        return result;
    }

    private static string ResolveCapability(EndpointInfo entity, string capability) =>
        capability.ToLowerInvariant() switch
        {
            "view" => entity.ReadCapability,
            "create" => entity.CreateCapability,
            "update" => entity.UpdateCapability,
            "delete" => entity.DeleteCapability,
            _ => capability.ToLowerInvariant()
        };

    private static string[] ResolveRequiredCapabilities(EndpointInfo entity, string capability) =>
        string.IsNullOrWhiteSpace(entity.AuthorizationFeature)
            ? []
            : [$"{entity.AuthorizationFeature}:{ResolveCapability(entity, capability)}"];

    private static int CapabilityActionMask(string capability) => capability.ToLowerInvariant() switch
    {
        "view" => 2 | 4,
        "create" => 1,
        "update" => 8,
        "delete" => 16,
        _ => 0
    };

    private static string ResolveActorRequirement(int actorRequirement) => actorRequirement switch
    {
        1 => "Optional",
        2 => "None",
        _ => "Required"
    };

    private static string ResolveTenantAccessMode(int tenantAccessMode) => tenantAccessMode switch
    {
        1 => "DelegatedTenant",
        2 => "Tenantless",
        _ => "ActorTenant"
    };

    private static string StringArrayLiteral(IEnumerable<string> values) =>
        $"[{string.Join(", ", values.Select(value => $"\"{Escape(value)}\""))}]";

    private static string DictionaryLiteral(
        IEnumerable<ActorAttributeInfo> attributes,
        int actionMask) =>
        $"new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.OrdinalIgnoreCase) {{ {string.Join(", ", attributes.Where(attribute => (attribute.Actions & actionMask) != 0).Select(attribute => $"[\"{Escape(attribute.Name)}\"] = \"{Escape(attribute.Value)}\""))} }}";

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
    public int ActorRequirement { get; set; }
    public int TenantAccessMode { get; set; }
    public string CrossTenantCapability { get; set; } = "identity.tenants:manage";
    public string? AuthorizationFeature { get; set; }
    public string ReadCapability { get; set; } = "view";
    public string CreateCapability { get; set; } = "create";
    public string UpdateCapability { get; set; } = "update";
    public string DeleteCapability { get; set; } = "delete";
    public List<ActorAttributeInfo> ActorAttributes { get; set; } = [];
    public bool IsBaseModel { get; set; }
    public bool HasCreateValidator { get; set; }
    public bool HasUpdateValidator { get; set; }
}

internal sealed class ActorAttributeInfo(string name, string value, int actions)
{
    public string Name { get; } = name;
    public string Value { get; } = value;
    public int Actions { get; } = actions;
}
