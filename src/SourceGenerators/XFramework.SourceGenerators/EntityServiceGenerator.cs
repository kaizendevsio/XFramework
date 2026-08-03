using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that creates service layer code for entities marked with [GenerateEndpoints].
/// Implements tenant isolation and navigation loading support.
/// Uses the incremental generator API for better performance.
/// </summary>
[Generator]
public class EntityServiceGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat ResponseTypeDisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

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

    private static ServiceInfo? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
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

            // Only generate service if Type is Service (1) or Both (3)
            if (type != 1 && type != 3)
                return null;

            // Check if entity implements IHasTenantId
            var hasTenantId = ImplementsTenantOwnership(classSymbol);
            var isBaseModel = InheritsBaseModel(classSymbol);

            return new ServiceInfo
            {
                ClassName = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                Actions = actions,
                HasTenantId = hasTenantId,
                AllowsGlobalTenantRows = AllowsGlobalTenantRows(classSymbol),
                IsBaseModel = isBaseModel,
                ExplicitResponseProperties = GetStringArrayValue(attributeData, "ResponseProperties")
            };
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ServiceInfo> entities, SourceProductionContext context)
    {
        // Combine local syntax-discovered entities with referenced assembly entities
        var allEntities = new List<ServiceInfo>();

        if (!entities.IsDefaultOrEmpty)
        {
            allEntities.AddRange(entities.Where(e => e != null));
        }

        // Discover entities from referenced assemblies
        var referencedEntities = DiscoverFromReferencedAssemblies(compilation);
        allEntities.AddRange(referencedEntities);

        // Deduplicate by class name
        var seen = new HashSet<string>();
        var generatedEntities = new List<ServiceInfo>();
        foreach (var entity in allEntities)
        {
            if (!seen.Add(entity.ClassName))
                continue;

            PopulateListFilters(compilation, entity);
            PopulateResponseProperties(compilation, entity);
            var source = GenerateServiceCode(entity);
            context.AddSource($"{entity.ClassName}Service.g.cs", SourceText.From(source, Encoding.UTF8));
            generatedEntities.Add(entity);
        }

        if (generatedEntities.Count > 0)
        {
            context.AddSource(
                "GeneratedEntityServiceRegistrations.g.cs",
                SourceText.From(GenerateServiceRegistrations(generatedEntities), Encoding.UTF8));
        }
    }

    private static string GenerateServiceRegistrations(IReadOnlyCollection<ServiceInfo> entities)
    {
        var registrations = string.Join(
            Environment.NewLine,
            entities.Select(entity =>
                $"        services.AddScoped<global::{entity.Namespace}.I{entity.ClassName}Service, global::{entity.Namespace}.{entity.ClassName}Service>();"));

        return $$"""
                 // <auto-generated/>
                 #nullable enable
                 using Microsoft.Extensions.DependencyInjection;

                 namespace XFramework.GeneratedServices;

                 public static class GeneratedEntityServiceRegistrations
                 {
                     public static IServiceCollection AddGeneratedEntityServices(this IServiceCollection services)
                     {
                 {{registrations}}
                         return services;
                     }
                 }
                 """;
    }

    private static void PopulateResponseProperties(Compilation compilation, ServiceInfo entity)
    {
        var entityType = compilation.GetTypeByMetadataName($"{entity.Namespace}.{entity.ClassName}");
        if (entityType is null)
            return;

        var hierarchy = new Stack<INamedTypeSymbol>();
        for (var current = entityType; current is not null; current = current.BaseType)
            hierarchy.Push(current);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var explicitProperties = entity.ExplicitResponseProperties is null
            ? null
            : new HashSet<string>(entity.ExplicitResponseProperties, StringComparer.Ordinal);
        while (hierarchy.Count > 0)
        {
            foreach (var property in hierarchy.Pop().GetMembers().OfType<IPropertySymbol>())
            {
                if (!seen.Add(property.Name) ||
                    property.IsStatic ||
                    property.IsIndexer ||
                    property.DeclaredAccessibility != Accessibility.Public ||
                    property.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                    explicitProperties is not null && !explicitProperties.Contains(property.Name) ||
                    !IsScalarResponseType(property.Type) ||
                    IsSerializationIgnored(property) ||
                    IsSensitiveOrBlobProperty(property.Name))
                {
                    continue;
                }

                entity.ResponseProperties.Add(new ResponsePropertyInfo(
                    property.Name,
                    property.Type.ToDisplayString(ResponseTypeDisplayFormat),
                    property.Type.IsReferenceType));
            }
        }
    }

    private static bool IsScalarResponseType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return false;

        if (type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullableType)
        {
            type = nullableType.TypeArguments[0];
        }

        return type.SpecialType switch
        {
            SpecialType.System_String => true,
            SpecialType.None => type.TypeKind == TypeKind.Enum || type.IsValueType,
            SpecialType.System_Object => false,
            _ => true
        };
    }

    private static bool IsSerializationIgnored(IPropertySymbol property) =>
        property.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.Name is "JsonIgnoreAttribute" or "MemoryPackIgnoreAttribute");

    private static bool IsSensitiveOrBlobProperty(string propertyName)
    {
        string[] blockedNameParts =
        [
            "Password",
            "Secret",
            "Token",
            "Hash",
            "Salt",
            "PrivateKey",
            "KeyMaterial",
            "FileBytes",
            "BinaryData",
            "BlobData",
            "Payload"
        ];

        return propertyName.Equals("SessionData", StringComparison.OrdinalIgnoreCase) ||
               blockedNameParts.Any(part =>
                   propertyName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void PopulateListFilters(Compilation compilation, ServiceInfo entity)
    {
        var entityType = compilation.GetTypeByMetadataName($"{entity.Namespace}.{entity.ClassName}");
        var requestType = compilation.GetTypeByMetadataName(
            $"{entity.Namespace}.Get{entity.ClassName}ListRequest");
        if (entityType is null || requestType is null)
            return;

        entity.HasSearchTerm = requestType.GetMembers("SearchTerm")
            .OfType<IPropertySymbol>()
            .Any(static property => property.Type.SpecialType == SpecialType.System_String);

        foreach (var requestProperty in requestType.GetMembers().OfType<IPropertySymbol>())
        {
            if (requestProperty.Name is "Page" or "PageSize" or "SearchTerm")
                continue;

            var entityProperty = FindProperty(entityType, requestProperty.Name);
            if (entityProperty is null || entityProperty.GetMethod is null)
                continue;

            if (requestProperty.Type.SpecialType == SpecialType.System_String &&
                entityProperty.Type.SpecialType == SpecialType.System_String)
            {
                entity.ListFilters.Add(new ListFilterInfo(requestProperty.Name, ListFilterKind.String));
                continue;
            }

            if (requestProperty.Type is not INamedTypeSymbol
                {
                    OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                    TypeArguments.Length: 1
                } nullableRequestType)
            {
                continue;
            }

            var requestValueType = nullableRequestType.TypeArguments[0];
            var nullableEntityType = entityProperty.Type as INamedTypeSymbol;
            var entityIsNullable = nullableEntityType is
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            };
            var entityValueType = entityIsNullable
                ? nullableEntityType!.TypeArguments[0]
                : entityProperty.Type;

            if (SymbolEqualityComparer.Default.Equals(requestValueType, entityValueType))
            {
                entity.ListFilters.Add(new ListFilterInfo(
                    requestProperty.Name,
                    entityIsNullable ? ListFilterKind.NullableValue : ListFilterKind.Value));
            }
        }
    }

    private static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault();
            if (property is not null)
                return property;
        }

        return null;
    }

    private static List<ServiceInfo> DiscoverFromReferencedAssemblies(Compilation compilation)
    {
        var results = new List<ServiceInfo>();
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

                    if (attrType != 1 && attrType != 3) // Service or Both
                        continue;

                    var isBaseModel = InheritsBaseModel(type);

                    results.Add(new ServiceInfo
                    {
                        ClassName = type.Name,
                        Namespace = type.ContainingNamespace.ToDisplayString(),
                        Actions = actions,
                        HasTenantId = ImplementsTenantOwnership(type),
                        AllowsGlobalTenantRows = AllowsGlobalTenantRows(type),
                        IsBaseModel = isBaseModel,
                        ExplicitResponseProperties = GetStringArrayValue(attr, "ResponseProperties")
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

    private static bool ImplementsTenantOwnership(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(static candidate =>
            candidate.Name == "IHasTenantId" &&
            candidate.ContainingNamespace.ToDisplayString() == "XFramework.Domain.Shared.Contracts.Base");

    private static bool AllowsGlobalTenantRows(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(static candidate =>
            candidate.Name == "IAllowsGlobalTenantRows" &&
            candidate.ContainingNamespace.ToDisplayString() == "XFramework.Domain.Shared.Contracts.Base");

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

    private static string GenerateServiceCode(ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member");
        sb.AppendLine();

        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using FluentValidation;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using Mapster;");
        sb.AppendLine("using XFramework.Core.Patterns;");
        sb.AppendLine("using XFramework.Core.Services;");
        sb.AppendLine("using XFramework.Domain.Shared.Contracts.Base;");
        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
        }

        GenerateResponseDto(sb, entity);

        // Service interface
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Auto-generated service interface for {entityName} entity.");
        sb.AppendLine($"    /// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public partial interface I{entityName}Service");
        sb.AppendLine("    {");
        
        if ((entity.Actions & 1) != 0) // Create
            sb.AppendLine($"        Task<Result<{entityName}>> CreateAsync(Create{entityName}Request request, CancellationToken ct = default);");
        if ((entity.Actions & 2) != 0) // Get
            sb.AppendLine($"        Task<Result<Generated{entityName}Response>> GetByIdAsync(Guid id, bool includeNavigations = false, int navigationDepth = 1, CancellationToken ct = default);");
        if ((entity.Actions & 4) != 0) // GetList
            sb.AppendLine($"        Task<Result<List<Generated{entityName}Response>>> GetListAsync(Get{entityName}ListRequest request, bool includeNavigations = false, int navigationDepth = 1, CancellationToken ct = default);");
        if ((entity.Actions & 8) != 0) // Update
            sb.AppendLine(entity.IsBaseModel
                ? $"        Task<Result<{entityName}>> UpdateAsync(Guid id, Guid expectedConcurrencyStamp, Update{entityName}Request request, CancellationToken ct = default);"
                : $"        Task<Result<{entityName}>> UpdateAsync(Guid id, Update{entityName}Request request, CancellationToken ct = default);");
        if ((entity.Actions & 16) != 0) // Delete
            sb.AppendLine(entity.IsBaseModel
                ? "        Task<Result> DeleteAsync(Guid id, Guid expectedConcurrencyStamp, CancellationToken ct = default);"
                : "        Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);");
        
        sb.AppendLine("    }");
        sb.AppendLine();

        // Service implementation
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Auto-generated service implementation for {entityName} entity.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public partial class {entityName}Service : I{entityName}Service");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly DbContext _context;");
        sb.AppendLine($"        private readonly IEnumerable<IValidator<{entityName}>> _entityValidators;");
        sb.AppendLine($"        private readonly ILogger<{entityName}Service> _logger;");
        if (entity.HasTenantId)
        {
            sb.AppendLine("        private readonly IHttpContextAccessor _httpContextAccessor;");
        }
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"        public {entityName}Service(");
        sb.AppendLine("            DbContext context,");
        if (entity.HasTenantId)
        {
            sb.AppendLine("            IHttpContextAccessor httpContextAccessor,");
            sb.AppendLine($"            IEnumerable<IValidator<{entityName}>> entityValidators,");
            sb.AppendLine($"            ILogger<{entityName}Service> logger)");
        }
        else
        {
            sb.AppendLine($"            IEnumerable<IValidator<{entityName}>> entityValidators,");
            sb.AppendLine($"            ILogger<{entityName}Service> logger)");
        }
        sb.AppendLine("        {");
        sb.AppendLine("            _context = context;");
        sb.AppendLine("            _entityValidators = entityValidators;");
        if (entity.HasTenantId)
        {
            sb.AppendLine("            _httpContextAccessor = httpContextAccessor;");
        }
        sb.AppendLine("            _logger = logger;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Tenant helper method
        if (entity.HasTenantId)
        {
            sb.AppendLine("        private Guid GetCurrentTenantId()");
            sb.AppendLine("        {");
            sb.AppendLine("            var principal = _httpContextAccessor.HttpContext?.User;");
            sb.AppendLine("            foreach (var claimName in new[] { \"tenant_id\", \"tenantId\", \"TenantId\", \"tid\", \"tenant\" })");
            sb.AppendLine("            {");
            sb.AppendLine("                if (Guid.TryParse(principal?.FindFirst(claimName)?.Value, out var tenantId) && tenantId != Guid.Empty)");
            sb.AppendLine("                    return tenantId;");
            sb.AppendLine("            }");
            sb.AppendLine("            throw new UnauthorizedAccessException(\"No valid tenant context found\");");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Generate CRUD methods
        if ((entity.Actions & 1) != 0) // Create
        {
            GenerateCreateMethod(sb, entity);
        }

        if ((entity.Actions & 2) != 0) // Get
        {
            GenerateGetByIdMethod(sb, entity);
        }

        if ((entity.Actions & 4) != 0) // GetList
        {
            GenerateGetListMethod(sb, entity);
        }

        if ((entity.Actions & 8) != 0) // Update
        {
            GenerateUpdateMethod(sb, entity);
        }

        if ((entity.Actions & 16) != 0) // Delete
        {
            GenerateDeleteMethod(sb, entity);
        }

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine("}");
        }

        sb.AppendLine();
        sb.AppendLine("#pragma warning restore CS1591");

        return sb.ToString();
    }

    private static void GenerateResponseDto(StringBuilder sb, ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Stable scalar response contract for {entityName}.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public sealed class Generated{entityName}Response");
        sb.AppendLine("    {");
        foreach (var property in entity.ResponseProperties)
        {
            var initializer = property.RequiresInitializer ? " = default!;" : string.Empty;
            sb.AppendLine($"        public {property.TypeName} {property.Name} {{ get; init; }}{initializer}");
        }

        sb.AppendLine();
        sb.AppendLine($"        internal static Generated{entityName}Response FromEntity({entityName} entity) => new()");
        sb.AppendLine("        {");
        foreach (var property in entity.ResponseProperties)
            sb.AppendLine($"            {property.Name} = entity.{property.Name}!,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendResponseProjection(StringBuilder sb, ServiceInfo entity, string indent)
    {
        sb.AppendLine($"{indent}.Select(static e => new Generated{entity.ClassName}Response");
        sb.AppendLine($"{indent}{{");
        foreach (var property in entity.ResponseProperties)
            sb.AppendLine($"{indent}    {property.Name} = e.{property.Name}!,");
        sb.AppendLine($"{indent}}})");
    }

    private static void GenerateCreateMethod(StringBuilder sb, ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        var hasTenantId = entity.HasTenantId;
        sb.AppendLine($"        /// <inheritdoc/>");
        sb.AppendLine($"        public virtual async Task<Result<{entityName}>> CreateAsync(Create{entityName}Request request, CancellationToken ct = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var entity = request.Adapt<{entityName}>();");
        sb.AppendLine("                entity.Id = Guid.NewGuid();");
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                entity.CreatedAt = DateTime.UtcNow;");
            sb.AppendLine("                entity.IsDeleted = false;");
            sb.AppendLine("                entity.ConcurrencyStamp = Guid.NewGuid();");
        }
        sb.AppendLine();
        if (hasTenantId)
        {
            sb.AppendLine("                var tenantId = GetCurrentTenantId();");
            sb.AppendLine("                ((IHasTenantId)entity).TenantId = tenantId;");
            sb.AppendLine();
        }
        AppendEntityValidation(sb, entityName);
        sb.AppendLine($"                _context.Set<{entityName}>().Add(entity);");
        sb.AppendLine("                await _context.SaveChangesAsync(ct);");
        sb.AppendLine($"                return Result<{entityName}>.Success(entity, 201, \"{entityName} created successfully\");");
        sb.AppendLine("            }");
        AppendUnauthorizedCatch(sb, $"Result<{entityName}>");
        AppendCancellationCatch(sb);
        AppendDatabaseConflictCatch(sb, $"Result<{entityName}>", entityName, "create");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        AppendExceptionLog(sb, entityName, "Create");
        sb.AppendLine($"                return Result<{entityName}>.Failure(\"Failed to create {entityName}.\", 500);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateGetByIdMethod(StringBuilder sb, ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        var hasTenantId = entity.HasTenantId;
        sb.AppendLine($"        /// <inheritdoc/>");
        sb.AppendLine($"        public virtual async Task<Result<Generated{entityName}Response>> GetByIdAsync(Guid id, bool includeNavigations = false, int navigationDepth = 1, CancellationToken ct = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var query = _context.Set<{entityName}>().AsNoTracking();");
        sb.AppendLine();
        if (hasTenantId)
        {
            sb.AppendLine("                var tenantId = GetCurrentTenantId();");
            sb.AppendLine(entity.AllowsGlobalTenantRows
                ? "                query = query.Where(e => ((IHasTenantId)e).TenantId == tenantId || ((IHasTenantId)e).TenantId == Guid.Empty);"
                : "                query = query.Where(e => ((IHasTenantId)e).TenantId == tenantId);");
            sb.AppendLine();
        }
        sb.AppendLine("                var response = await query");
        sb.AppendLine("                    .Where(e => e.Id == id)");
        AppendResponseProjection(sb, entity, "                    ");
        sb.AppendLine("                    .FirstOrDefaultAsync(ct);");
        sb.AppendLine("                if (response == null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    return Result<Generated{entityName}Response>.NotFound($\"{entityName} with ID {{id}} not found\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine($"                return Result<Generated{entityName}Response>.Success(response);");
        sb.AppendLine("            }");
        AppendUnauthorizedCatch(sb, $"Result<Generated{entityName}Response>");
        AppendCancellationCatch(sb);
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        AppendExceptionLog(sb, entityName, "GetById");
        sb.AppendLine($"                return Result<Generated{entityName}Response>.Failure(\"Failed to retrieve {entityName}.\", 500);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateGetListMethod(StringBuilder sb, ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        var hasTenantId = entity.HasTenantId;
        sb.AppendLine($"        /// <inheritdoc/>");
        sb.AppendLine($"        public virtual async Task<Result<List<Generated{entityName}Response>>> GetListAsync(Get{entityName}ListRequest request, bool includeNavigations = false, int navigationDepth = 1, CancellationToken ct = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                var query = _context.Set<{entityName}>().AsNoTracking();");
        sb.AppendLine();
        if (hasTenantId)
        {
            sb.AppendLine("                var tenantId = GetCurrentTenantId();");
            sb.AppendLine(entity.AllowsGlobalTenantRows
                ? "                query = query.Where(e => ((IHasTenantId)e).TenantId == tenantId || ((IHasTenantId)e).TenantId == Guid.Empty);"
                : "                query = query.Where(e => ((IHasTenantId)e).TenantId == tenantId);");
            sb.AppendLine();
        }
        foreach (var filter in entity.ListFilters)
        {
            switch (filter.Kind)
            {
                case ListFilterKind.String:
                    sb.AppendLine($"                if (!string.IsNullOrWhiteSpace(request.{filter.Name}))");
                    sb.AppendLine($"                    query = query.Where(e => e.{filter.Name} == request.{filter.Name});");
                    break;
                case ListFilterKind.NullableValue:
                    sb.AppendLine($"                if (request.{filter.Name}.HasValue)");
                    sb.AppendLine($"                    query = query.Where(e => e.{filter.Name} == request.{filter.Name});");
                    break;
                case ListFilterKind.Value:
                    sb.AppendLine($"                if (request.{filter.Name}.HasValue)");
                    sb.AppendLine($"                    query = query.Where(e => e.{filter.Name} == request.{filter.Name}.Value);");
                    break;
            }
            sb.AppendLine();
        }
        if (entity.HasSearchTerm)
        {
            sb.AppendLine("                if (!string.IsNullOrWhiteSpace(request.SearchTerm))");
            sb.AppendLine($"                    return Result<List<Generated{entityName}Response>>.Failure(\"SearchTerm is not supported for generated {entityName} lists because searchable fields are not explicitly defined.\", 400);");
            sb.AppendLine();
        }
        sb.AppendLine("                // Apply deterministic ordering before pagination");
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                query = query.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id);");
        }
        else
        {
            sb.AppendLine("                query = query.OrderBy(e => e.Id);");
        }
        sb.AppendLine("                var page = Math.Max(1, request.Page);");
        sb.AppendLine("                var pageSize = Math.Clamp(request.PageSize, 1, 100);");
        sb.AppendLine("                var skip = (long)(page - 1) * pageSize;");
        sb.AppendLine("                if (skip > int.MaxValue)");
        sb.AppendLine($"                    return Result<List<Generated{entityName}Response>>.Failure(\"Requested page is outside the supported range.\", 400);");
        sb.AppendLine("                var responses = await query");
        sb.AppendLine("                    .Skip((int)skip)");
        sb.AppendLine("                    .Take(pageSize)");
        AppendResponseProjection(sb, entity, "                    ");
        sb.AppendLine("                    .ToListAsync(ct);");
        sb.AppendLine();
        sb.AppendLine($"                return Result<List<Generated{entityName}Response>>.Success(responses);");
        sb.AppendLine("            }");
        AppendUnauthorizedCatch(sb, $"Result<List<Generated{entityName}Response>>");
        AppendCancellationCatch(sb);
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        AppendExceptionLog(sb, entityName, "GetList");
        sb.AppendLine($"                return Result<List<Generated{entityName}Response>>.Failure(\"Failed to retrieve {entityName} list.\", 500);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateUpdateMethod(StringBuilder sb, ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        var hasTenantId = entity.HasTenantId;
        sb.AppendLine($"        /// <inheritdoc/>");
        sb.AppendLine(entity.IsBaseModel
            ? $"        public virtual async Task<Result<{entityName}>> UpdateAsync(Guid id, Guid expectedConcurrencyStamp, Update{entityName}Request request, CancellationToken ct = default)"
            : $"        public virtual async Task<Result<{entityName}>> UpdateAsync(Guid id, Update{entityName}Request request, CancellationToken ct = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                if (expectedConcurrencyStamp == Guid.Empty)");
            sb.AppendLine($"                    return Result<{entityName}>.Failure(\"Expected concurrency stamp is required.\", 400);");
            sb.AppendLine();
        }
        sb.AppendLine($"                var query = _context.Set<{entityName}>().AsTracking();");
        if (hasTenantId)
        {
            sb.AppendLine("                var tenantId = GetCurrentTenantId();");
            sb.AppendLine("                var entity = await query.FirstOrDefaultAsync(e => e.Id == id && ((IHasTenantId)e).TenantId == tenantId, ct);");
        }
        else
        {
            sb.AppendLine("                var entity = await query.FirstOrDefaultAsync(e => e.Id == id, ct);");
        }
        sb.AppendLine("                if (entity == null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    return Result<{entityName}>.NotFound($\"{entityName} with ID {{id}} not found\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                if (entity.ConcurrencyStamp != expectedConcurrencyStamp)");
            sb.AppendLine($"                    return Result<{entityName}>.Conflict(\"{entityName} was modified by another request.\");");
            sb.AppendLine();
            sb.AppendLine("                _context.Entry(entity).Property(e => e.ConcurrencyStamp).OriginalValue = expectedConcurrencyStamp;");
        }
        sb.AppendLine("                request.Adapt(entity);");
        if (hasTenantId)
        {
            sb.AppendLine("                ((IHasTenantId)entity).TenantId = tenantId;");
        }
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                entity.ModifiedAt = DateTime.UtcNow;");
            sb.AppendLine("                entity.ConcurrencyStamp = Guid.NewGuid();");
        }
        sb.AppendLine();
        AppendEntityValidation(sb, entityName);
        sb.AppendLine("                await _context.SaveChangesAsync(ct);");
        sb.AppendLine($"                return Result<{entityName}>.Success(entity, \"{entityName} updated successfully\");");
        sb.AppendLine("            }");
        AppendUnauthorizedCatch(sb, $"Result<{entityName}>");
        AppendCancellationCatch(sb);
        if (entity.IsBaseModel)
        {
            sb.AppendLine("            catch (DbUpdateConcurrencyException ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                _logger.LogWarning(ex, \"Generated entity update conflicted for {{EntityType}}\", nameof({entityName}));");
            sb.AppendLine($"                return Result<{entityName}>.Conflict(\"{entityName} was modified by another request.\");");
            sb.AppendLine("            }");
        }
        AppendDatabaseConflictCatch(sb, $"Result<{entityName}>", entityName, "update");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        AppendExceptionLog(sb, entityName, "Update");
        sb.AppendLine($"                return Result<{entityName}>.Failure(\"Failed to update {entityName}.\", 500);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateDeleteMethod(StringBuilder sb, ServiceInfo entity)
    {
        var entityName = entity.ClassName;
        var hasTenantId = entity.HasTenantId;
        sb.AppendLine($"        /// <inheritdoc/>");
        sb.AppendLine(entity.IsBaseModel
            ? "        public virtual async Task<Result> DeleteAsync(Guid id, Guid expectedConcurrencyStamp, CancellationToken ct = default)"
            : "        public virtual async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                if (expectedConcurrencyStamp == Guid.Empty)");
            sb.AppendLine("                    return Result.Failure(\"Expected concurrency stamp is required.\", 400);");
            sb.AppendLine();
        }
        sb.AppendLine($"                var query = _context.Set<{entityName}>().AsTracking();");
        if (hasTenantId)
        {
            sb.AppendLine("                var tenantId = GetCurrentTenantId();");
            sb.AppendLine("                var entity = await query.FirstOrDefaultAsync(e => e.Id == id && ((IHasTenantId)e).TenantId == tenantId, ct);");
        }
        else
        {
            sb.AppendLine("                var entity = await query.FirstOrDefaultAsync(e => e.Id == id, ct);");
        }
        sb.AppendLine("                if (entity == null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    return Result.NotFound($\"{entityName} with ID {{id}} not found\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        if (entity.IsBaseModel)
        {
            sb.AppendLine("                if (entity.ConcurrencyStamp != expectedConcurrencyStamp)");
            sb.AppendLine($"                    return Result.Conflict(\"{entityName} was modified by another request.\");");
            sb.AppendLine();
            sb.AppendLine("                _context.Entry(entity).Property(e => e.ConcurrencyStamp).OriginalValue = expectedConcurrencyStamp;");
        }
        sb.AppendLine($"                _context.Set<{entityName}>().Remove(entity);");
        sb.AppendLine("                await _context.SaveChangesAsync(ct);");
        sb.AppendLine($"                return Result.Success(\"{entityName} deleted successfully\");");
        sb.AppendLine("            }");
        AppendUnauthorizedCatch(sb, "Result");
        AppendCancellationCatch(sb);
        if (entity.IsBaseModel)
        {
            sb.AppendLine("            catch (DbUpdateConcurrencyException ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                _logger.LogWarning(ex, \"Generated entity delete conflicted for {{EntityType}}\", nameof({entityName}));");
            sb.AppendLine($"                return Result.Conflict(\"{entityName} was modified by another request.\");");
            sb.AppendLine("            }");
        }
        AppendDatabaseConflictCatch(sb, "Result", entityName, "delete");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        AppendExceptionLog(sb, entityName, "Delete");
        sb.AppendLine($"                return Result.Failure(\"Failed to delete {entityName}.\", 500);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void AppendExceptionLog(StringBuilder sb, string entityName, string operation)
    {
        sb.AppendLine(
            $"                _logger.LogError(ex, \"Generated entity operation {{Operation}} failed for {{EntityType}}\", \"{operation}\", nameof({entityName}));");
    }

    private static void AppendCancellationCatch(StringBuilder sb)
    {
        sb.AppendLine("            catch (OperationCanceledException) when (ct.IsCancellationRequested)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw;");
        sb.AppendLine("            }");
    }

    private static void AppendDatabaseConflictCatch(
        StringBuilder sb,
        string resultType,
        string entityName,
        string operation)
    {
        sb.AppendLine("            catch (DbUpdateException ex)");
        sb.AppendLine("            {");
        sb.AppendLine($"                _logger.LogWarning(ex, \"Generated entity {operation} conflicted for {{EntityType}}\", nameof({entityName}));");
        sb.AppendLine($"                return {resultType}.Conflict(\"{entityName} conflicts with an existing record.\");");
        sb.AppendLine("            }");
    }

    private static void AppendUnauthorizedCatch(StringBuilder sb, string resultType)
    {
        sb.AppendLine("            catch (UnauthorizedAccessException ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                _logger.LogWarning(ex, \"Generated entity operation rejected because tenant context is missing\");");
        sb.AppendLine($"                return {resultType}.Unauthorized(\"A valid tenant context is required.\");");
        sb.AppendLine("            }");
    }

    private static void AppendEntityValidation(StringBuilder sb, string entityName)
    {
        sb.AppendLine("                foreach (var entityValidator in _entityValidators)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var validationResult = await entityValidator.ValidateAsync(entity, ct);");
        sb.AppendLine("                    if (!validationResult.IsValid)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var message = string.Join(\"; \", validationResult.Errors.Select(static error => error.ErrorMessage).Distinct());");
        sb.AppendLine($"                        return Result<{entityName}>.Failure(message, 400);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
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

    private static string[]? GetStringArrayValue(AttributeData attributeData, string propertyName)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key != propertyName || namedArgument.Value.IsNull)
                continue;

            return namedArgument.Value.Values
                .Select(static value => value.Value as string)
                .Where(static value => value is not null)
                .Cast<string>()
                .ToArray();
        }

        return null;
    }
}

internal class ServiceInfo
{
    public string ClassName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public int Actions { get; set; }
    public bool IsBaseModel { get; set; }
    public bool HasTenantId { get; set; }
    public bool AllowsGlobalTenantRows { get; set; }
    public bool HasSearchTerm { get; set; }
    public List<ListFilterInfo> ListFilters { get; } = [];
    public List<ResponsePropertyInfo> ResponseProperties { get; } = [];
    public string[]? ExplicitResponseProperties { get; set; }
}

internal sealed class ResponsePropertyInfo(string name, string typeName, bool requiresInitializer)
{
    public string Name { get; } = name;
    public string TypeName { get; } = typeName;
    public bool RequiresInitializer { get; } = requiresInitializer;
}

internal sealed class ListFilterInfo(string name, ListFilterKind kind)
{
    public string Name { get; } = name;
    public ListFilterKind Kind { get; } = kind;
}

internal enum ListFilterKind
{
    String,
    NullableValue,
    Value
}
