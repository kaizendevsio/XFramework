using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that collects entity types marked with [GenerateEndpoints] and generates
/// a static registry for DataContext entity allowlisting. The generated code has no external
/// dependencies — it returns a dictionary of (name → Type) that the Bolt server-side
/// QueryExecutionService consumes at startup.
/// </summary>
[Generator]
public class DataContextRegistrationGenerator : IIncrementalGenerator
{
    private const int MutatingEndpointActions = 1 | 8 | 16; // Create | Update | Delete
    private const string CoreGenerateEndpointsAttribute = "XFramework.Core.Attributes.GenerateEndpointsAttribute";
    private const string SharedGenerateEndpointsAttribute = "XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute";
    private const string SharedAllowRemoteMutationAttribute = "XFramework.Domain.Shared.Attributes.AllowRemoteDataContextMutationAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GetEntityInfo(ctx))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static EntityRegistrationInfo? GetEntityInfo(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return null;

        foreach (var attributeData in classSymbol.GetAttributes())
        {
            var attributeClass = attributeData.AttributeClass;
            if (attributeClass == null)
                continue;

            if (attributeClass.Name != "GenerateEndpointsAttribute" &&
                attributeClass.ToDisplayString() != "XFramework.Core.Attributes.GenerateEndpointsAttribute")
                continue;

            return new EntityRegistrationInfo
            {
                ClassName = classSymbol.Name,
                FullyQualifiedName = classSymbol.ToDisplayString(),
                AssemblyName = classSymbol.ContainingAssembly?.Name ?? "",
                EndpointActionsValue = GetEndpointActionsValue(attributeData),
                AllowRemoteMutation = HasAllowRemoteMutationAttribute(classSymbol)
            };
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<EntityRegistrationInfo?> entities, SourceProductionContext context)
    {
        var allEntities = new List<EntityRegistrationInfo>();
        var currentModuleName = GetModuleName(compilation.AssemblyName ?? string.Empty);

        if (!entities.IsDefaultOrEmpty)
        {
            allEntities.AddRange(entities
                .Where(e => e is not null && ShouldIncludeEntity(e!.AssemblyName, currentModuleName))
                .Select(e => e!));
        }

        // Discover from referenced assemblies
        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is not IAssemblySymbol assembly)
                continue;

            foreach (var type in GetAllTypes(assembly.GlobalNamespace))
            {
                if (type.IsAbstract || type.TypeKind != TypeKind.Class)
                    continue;

                if (!ShouldIncludeEntity(assembly.Name, currentModuleName))
                    continue;

                var generateEndpointsAttribute = type.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.ToDisplayString() == CoreGenerateEndpointsAttribute ||
                    a.AttributeClass?.ToDisplayString() == SharedGenerateEndpointsAttribute);

                if (generateEndpointsAttribute is not null)
                {
                    allEntities.Add(new EntityRegistrationInfo
                    {
                        ClassName = type.Name,
                        FullyQualifiedName = type.ToDisplayString(),
                        AssemblyName = assembly.Name,
                        EndpointActionsValue = GetEndpointActionsValue(generateEndpointsAttribute),
                        AllowRemoteMutation = HasAllowRemoteMutationAttribute(type)
                    });
                }
            }
        }

        var validEntities = allEntities
            .Where(e => e is not null)
            .Select(e => e!)
            .GroupBy(e => e.FullyQualifiedName)
            .Select(g => g.First())
            .OrderBy(e => e.ClassName)
            .ToList();

        if (validEntities.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("namespace XFramework.Core.DataContext;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated entity type registry for DataContext.");
        sb.AppendLine("/// Call GetDataContextEntityTypes() to get the allowlist for QueryExecutionService.");
        sb.AppendLine("/// Call GetDataContextServiceWrapperMap() to get entity-to-service-wrapper routing.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class DataContextEntityRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    public static Dictionary<string, Type> GetDataContextEntityTypes() => new()");
        sb.AppendLine("    {");

        foreach (var entity in validEntities)
        {
            sb.AppendLine($"        [\"{entity.ClassName}\"] = typeof(global::{entity.FullyQualifiedName}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    public static HashSet<string> GetDataContextMutableEntityTypes() => new(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("    {");
        foreach (var entity in validEntities.Where(static e => HasMutatingEndpointActions(e.EndpointActionsValue) || e.AllowRemoteMutation))
        {
            sb.AppendLine($"        \"{entity.ClassName}\",");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        // Generate GetDataContextServiceWrapperMap() — maps entity names to their module's wrapper type name.
        // The wrapper types are source-generated in Integration projects, so they may not be resolvable
        // via typeof() at compile time. We emit string-based metadata names that the consumer resolves
        // at runtime from loaded assemblies (e.g., via Type.GetType() or assembly scanning).
        var wrapperMappings = new List<(string EntityName, string WrapperMetadataName)>();
        foreach (var entity in validEntities)
        {
            if (string.IsNullOrEmpty(entity.AssemblyName))
                continue;

            var moduleName = GetOwningModuleName(entity.AssemblyName, currentModuleName);
            var wrapperMetadataName = $"{moduleName}.Integration.Drivers.I{moduleName}ServiceWrapper";

            // Try to resolve from compilation first (covers pre-compiled referenced assemblies)
            var wrapperSymbol = compilation.GetTypeByMetadataName(wrapperMetadataName);
            if (wrapperSymbol != null)
            {
                wrapperMappings.Add((entity.ClassName, wrapperSymbol.ToDisplayString()));
            }
            else
            {
                // Wrapper is likely source-generated in the same or a peer compilation —
                // emit the conventional name so the consumer can resolve at runtime
                wrapperMappings.Add((entity.ClassName, wrapperMetadataName));
            }
        }

        sb.AppendLine("    public static Dictionary<string, string> GetDataContextServiceWrapperMap() => new()");
        sb.AppendLine("    {");
        foreach (var (entityName, wrapperName) in wrapperMappings)
        {
            sb.AppendLine($"        [\"{entityName}\"] = \"{wrapperName}\",");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");

        context.AddSource("DataContextEntityRegistrations.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GetModuleName(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return string.Empty;

        return assemblyName.Split('.')[0];
    }

    private static int GetEndpointActionsValue(AttributeData attributeData)
    {
        foreach (var argument in attributeData.NamedArguments)
        {
            if (argument.Key == "Actions" && argument.Value.Value is int value)
                return value;
        }

        return 31; // EndpointActions.All
    }

    private static bool HasMutatingEndpointActions(int endpointActionsValue) =>
        (endpointActionsValue & MutatingEndpointActions) != 0;

    private static bool HasAllowRemoteMutationAttribute(INamedTypeSymbol type) =>
        type.GetAttributes().Any(static a =>
            a.AttributeClass?.ToDisplayString() == SharedAllowRemoteMutationAttribute);

    private static bool ShouldIncludeEntity(string assemblyName, string currentModuleName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName) || string.IsNullOrWhiteSpace(currentModuleName))
            return true;

        if (assemblyName.StartsWith(currentModuleName, StringComparison.OrdinalIgnoreCase))
            return true;

        return currentModuleName.Equals("IdentityServer", StringComparison.OrdinalIgnoreCase)
               && assemblyName.StartsWith("XFramework.Domain.Shared", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetOwningModuleName(string entityAssemblyName, string currentModuleName)
    {
        if (currentModuleName.Equals("IdentityServer", StringComparison.OrdinalIgnoreCase)
            && entityAssemblyName.StartsWith("XFramework.Domain.Shared", StringComparison.OrdinalIgnoreCase))
        {
            return "IdentityServer";
        }

        return GetModuleName(entityAssemblyName);
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

    private class EntityRegistrationInfo
    {
        public string ClassName { get; set; } = "";
        public string FullyQualifiedName { get; set; } = "";
        public string AssemblyName { get; set; } = "";
        public int EndpointActionsValue { get; set; } = 31;
        public bool AllowRemoteMutation { get; set; }
    }
}
