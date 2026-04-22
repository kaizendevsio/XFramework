using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XFramework.SourceGenerators;

/// <summary>
/// Source generator that creates per-entity snapshot records, change trackers, and a registry
/// for entities marked with [GenerateEndpoints]. Used by RemoteDataContext to produce FieldPatch
/// diffs for efficient wire-format updates.
///
/// For each entity, generates:
/// 1. {Entity}Snapshot — MemoryPackable record holding copies of all scalar properties
/// 2. {Entity}ChangeTracker : IEntityChangeTracker&lt;{Entity}&gt; — Snapshot(), Diff(), GetPrimaryKey()
/// 3. ChangeTrackerRegistry — static Type → tracker instance lookup
///
/// Runs only in *.Integration projects (by assembly name convention).
/// Discovers entities from referenced assemblies via cross-assembly scanning.
/// </summary>
[Generator]
public class ChangeTrackerGenerator : IIncrementalGenerator
{
    // SpecialType values that are known scalar types (covers keyword aliases like string, bool, int, etc.)
    private static readonly HashSet<SpecialType> ScalarSpecialTypes = new()
    {
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_Single,
        SpecialType.System_Double,
        SpecialType.System_Decimal,
        SpecialType.System_String,
        SpecialType.System_DateTime
    };

    // Non-special scalar types that need FQN matching (no SpecialType enum value in Roslyn).
    private static readonly HashSet<string> ScalarMetadataNames = new()
    {
        "System.Guid",
        "System.DateTimeOffset",
        "System.DateOnly",
        "System.TimeOnly",
        "System.TimeSpan"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pure auto-discovery: use CompilationProvider to scan referenced assemblies
        // for [GenerateEndpoints] entities. Triggers only in *.Integration projects.
        context.RegisterSourceOutput(context.CompilationProvider,
            static (spc, compilation) => Execute(compilation, spc));
    }

    private static void Execute(Compilation compilation, SourceProductionContext context)
    {
        var assemblyName = compilation.AssemblyName ?? "";

        // Only run in Integration projects
        if (!assemblyName.EndsWith(".Integration"))
            return;

        var moduleName = assemblyName.Split('.').First();

        // Discover entities with [GenerateEndpoints] from referenced assemblies
        var entities = DiscoverEntities(compilation, moduleName);

        if (entities.Count == 0)
            return;

        // Generate per-entity snapshot + change tracker files
        foreach (var entity in entities)
        {
            var source = GenerateEntityChangeTracker(entity);
            context.AddSource(
                $"{entity.ClassName}ChangeTracker.g.cs",
                SourceText.From(source, Encoding.UTF8));
        }

        // Generate the registry
        var registrySource = GenerateRegistry(entities);
        context.AddSource(
            "ChangeTrackerRegistry.g.cs",
            SourceText.From(registrySource, Encoding.UTF8));
    }

    private static List<EntityInfo> DiscoverEntities(Compilation compilation, string moduleName)
    {
        var entities = new List<EntityInfo>();
        var seen = new HashSet<string>();
        var coreAttr = "XFramework.Core.Attributes.GenerateEndpointsAttribute";
        var sharedAttr = "XFramework.Domain.Shared.Attributes.GenerateEndpointsAttribute";

        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is not IAssemblySymbol assembly)
                continue;

            // Scan module's own assemblies
            // Also scan XFramework.Domain.Shared for IdentityServer (manages StorageFile etc.)
            var isModuleAssembly = assembly.Name.StartsWith(moduleName, StringComparison.OrdinalIgnoreCase);
            var isSharedForIdentity = moduleName == "IdentityServer" &&
                assembly.Name.StartsWith("XFramework.Domain.Shared", StringComparison.OrdinalIgnoreCase);
            if (!isModuleAssembly && !isSharedForIdentity)
                continue;

            foreach (var type in GetTypesFromNamespace(assembly.GlobalNamespace))
            {
                if (type.IsAbstract || type.TypeKind != TypeKind.Class)
                    continue;

                var hasAttribute = false;
                foreach (var attr in type.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();
                    if (attrName == coreAttr || attrName == sharedAttr)
                    {
                        hasAttribute = true;
                        break;
                    }
                }

                if (!hasAttribute)
                    continue;

                var fqn = type.ToDisplayString();
                if (!seen.Add(fqn))
                    continue;

                var scalarProps = GetScalarProperties(type);
                if (scalarProps.Count == 0)
                    continue;

                entities.Add(new EntityInfo
                {
                    ClassName = type.Name,
                    Namespace = type.ContainingNamespace.ToDisplayString(),
                    FullyQualifiedName = fqn,
                    ScalarProperties = scalarProps
                });
            }
        }

        entities.Sort((a, b) => string.Compare(a.ClassName, b.ClassName, StringComparison.Ordinal));
        return entities;
    }

    /// <summary>
    /// Collects all public instance properties with setters that are scalar types,
    /// excluding the "Id" property (PK). Walks the full inheritance hierarchy.
    /// </summary>
    private static List<PropertyInfo> GetScalarProperties(INamedTypeSymbol type)
    {
        var props = new List<PropertyInfo>();
        var seen = new HashSet<string>();
        var order = 0;

        // Walk the type hierarchy (most-derived first)
        var current = type;
        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol prop)
                    continue;

                // Public instance properties with setters only
                if (prop.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (prop.IsStatic || prop.IsIndexer || prop.IsReadOnly)
                    continue;
                if (prop.SetMethod == null)
                    continue;

                // Skip Id (PK)
                if (prop.Name == "Id")
                    continue;

                // Deduplicate (overrides in derived types)
                if (!seen.Add(prop.Name))
                    continue;

                // Check if the property type is scalar
                if (!IsScalarType(prop.Type))
                    continue;

                var fullyQualifiedType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                props.Add(new PropertyInfo
                {
                    Name = prop.Name,
                    FullyQualifiedTypeName = fullyQualifiedType,
                    Order = order++
                });
            }

            current = current.BaseType;
        }

        return props;
    }

    /// <summary>
    /// Determines whether a type symbol represents a scalar type suitable for change tracking.
    /// </summary>
    private static bool IsScalarType(ITypeSymbol typeSymbol)
    {
        // Handle nullable value types: Nullable<T>
        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsScalarType(namedType.TypeArguments[0]);
        }

        // Enums are scalar
        if (typeSymbol.TypeKind == TypeKind.Enum)
            return true;

        // byte[] (array of System.Byte) is scalar
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType.SpecialType == SpecialType.System_Byte;
        }

        // Check SpecialType first (covers string, bool, int, decimal, DateTime, etc.)
        if (ScalarSpecialTypes.Contains(typeSymbol.SpecialType))
            return true;

        // Check non-special scalar types by metadata name (Guid, DateTimeOffset, DateOnly, TimeOnly, TimeSpan)
        var metadataName = GetFullMetadataName(typeSymbol);
        return ScalarMetadataNames.Contains(metadataName);
    }

    /// <summary>
    /// Gets the full metadata name (e.g. "System.Guid") for a type symbol, which is stable
    /// regardless of C# keyword aliases or display format settings.
    /// </summary>
    private static string GetFullMetadataName(ITypeSymbol symbol)
    {
        if (symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace)
            return symbol.MetadataName;
        return $"{symbol.ContainingNamespace.ToDisplayString()}.{symbol.MetadataName}";
    }

    private static string GenerateEntityChangeTracker(EntityInfo entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using MemoryPack;");
        sb.AppendLine("using XFramework.Domain.Shared.DataContext;");
        sb.AppendLine();
        sb.AppendLine($"namespace {entity.Namespace};");
        sb.AppendLine();

        // Snapshot record
        sb.AppendLine("[MemoryPackable]");
        sb.AppendLine($"public partial record {entity.ClassName}Snapshot");
        sb.AppendLine("{");
        foreach (var prop in entity.ScalarProperties)
        {
            sb.AppendLine($"    [MemoryPackOrder({prop.Order})] public {prop.FullyQualifiedTypeName} {prop.Name} {{ get; init; }}");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // ChangeTracker class
        var globalEntity = $"global::{entity.FullyQualifiedName}";
        sb.AppendLine($"public sealed class {entity.ClassName}ChangeTracker : IEntityChangeTracker<{globalEntity}>");
        sb.AppendLine("{");

        // GetPrimaryKey
        sb.AppendLine($"    public System.Guid GetPrimaryKey({globalEntity} entity) => entity.Id;");
        sb.AppendLine();

        // Snapshot
        sb.AppendLine($"    public object Snapshot({globalEntity} entity) => new {entity.ClassName}Snapshot");
        sb.AppendLine("    {");
        foreach (var prop in entity.ScalarProperties)
        {
            sb.AppendLine($"        {prop.Name} = entity.{prop.Name},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        // Diff
        sb.AppendLine($"    public FieldPatch? Diff({globalEntity} current, object snapshotObj)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var original = ({entity.ClassName}Snapshot)snapshotObj;");
        sb.AppendLine("        var changes = new System.Collections.Generic.Dictionary<string, byte[]>();");
        sb.AppendLine();
        foreach (var prop in entity.ScalarProperties)
        {
            sb.AppendLine($"        if (!System.Collections.Generic.EqualityComparer<{prop.FullyQualifiedTypeName}>.Default.Equals(current.{prop.Name}, original.{prop.Name}))");
            sb.AppendLine($"            changes[\"{prop.Name}\"] = MemoryPack.MemoryPackSerializer.Serialize(current.{prop.Name});");
        }
        sb.AppendLine();
        sb.AppendLine("        if (changes.Count == 0) return null;");
        sb.AppendLine();
        sb.AppendLine("        return new FieldPatch");
        sb.AppendLine("        {");
        sb.AppendLine("            EntityId = MemoryPack.MemoryPackSerializer.Serialize(current.Id),");
        sb.AppendLine("            Changes = changes");
        sb.AppendLine("        };");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRegistry(List<EntityInfo> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using XFramework.Domain.Shared.DataContext;");
        sb.AppendLine();
        sb.AppendLine("namespace XFramework.Integration.DataContext;");
        sb.AppendLine();
        sb.AppendLine("public static class ChangeTrackerRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly System.Collections.Generic.Dictionary<System.Type, object> Trackers = new()");
        sb.AppendLine("    {");
        foreach (var entity in entities)
        {
            sb.AppendLine($"        [typeof(global::{entity.FullyQualifiedName})] = new global::{entity.Namespace}.{entity.ClassName}ChangeTracker(),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    public static IEntityChangeTracker<T> GetTracker<T>() where T : class");
        sb.AppendLine("        => (IEntityChangeTracker<T>)Trackers[typeof(T)];");
        sb.AppendLine();
        sb.AppendLine("    public static bool HasTracker<T>() where T : class");
        sb.AppendLine("        => Trackers.ContainsKey(typeof(T));");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static IEnumerable<INamedTypeSymbol> GetTypesFromNamespace(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;
        foreach (var childNs in ns.GetNamespaceMembers())
            foreach (var type in GetTypesFromNamespace(childNs))
                yield return type;
    }

    private class EntityInfo
    {
        public string ClassName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string FullyQualifiedName { get; set; } = "";
        public List<PropertyInfo> ScalarProperties { get; set; } = new();
    }

    private class PropertyInfo
    {
        public string Name { get; set; } = "";
        public string FullyQualifiedTypeName { get; set; } = "";
        public int Order { get; set; }
    }
}
