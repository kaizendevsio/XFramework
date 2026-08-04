using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using XFramework.SourceGenerators;

namespace XFramework.SourceGenerators.Tests;

[TestFixture]
public sealed class EntityServiceGeneratorTests
{
    [Test]
    public void GenerateService_ListClampsPageAndPageSizeBeforeQuerying()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("var page = Math.Max(1, request.Page);");
        generatedSource.Should().Contain("var pageSize = Math.Clamp(request.PageSize, 1, 100);");
        generatedSource.Should().Contain("var skip = (long)(page - 1) * pageSize;");
        generatedSource.Should().Contain("if (skip > int.MaxValue)");
        generatedSource.Should().Contain(".Skip((int)skip)");
        generatedSource.Should().Contain(".Take(pageSize)");
        generatedSource.Should().Contain(".ToListAsync(ct)");
        generatedSource.Should().NotContain("Take(request.PageSize)");
    }

    [Test]
    public void GenerateService_ReadsProjectStableScalarResponseAtDatabaseBoundary()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("public sealed class GeneratedWidgetResponse");
        generatedSource.Should().Contain("Task<Result<GeneratedWidgetResponse>> GetByIdAsync");
        generatedSource.Should().Contain("Task<Result<List<GeneratedWidgetResponse>>> GetListAsync");
        generatedSource.Should().Contain(".Select(static e => new GeneratedWidgetResponse", Exactly.Times(2));
        generatedSource.Should().Contain("Id = e.Id", Exactly.Times(2));
        generatedSource.Should().Contain("Name = e.Name", Exactly.Times(2));
        generatedSource.Should().Contain("EffectiveDate = e.EffectiveDate", Exactly.Times(2));
        generatedSource.Should().NotContain("IncludeNavigations(");
        generatedSource.Should().NotContain("AsSplitQuery()");
    }

    [Test]
    public void GenerateService_ResponseExcludesNavigationsIgnoredMembersAndSecretOrBlobFields()
    {
        var generatedSource = RunGenerator();
        var responseStart = generatedSource.IndexOf("public sealed class GeneratedWidgetResponse", StringComparison.Ordinal);
        var responseEnd = generatedSource.IndexOf("public partial interface IWidgetService", StringComparison.Ordinal);
        var responseContract = generatedSource[responseStart..responseEnd];

        responseContract.Should().Contain("Name { get; init; }");
        responseContract.Should().Contain("string? Description { get; init; }");
        responseContract.Should().Contain("EffectiveDate { get; init; }");
        responseContract.Should().NotContain(" Owner { get; init; }");
        responseContract.Should().NotContain(" Children { get; init; }");
        responseContract.Should().NotContain("JsonIgnoredValue");
        responseContract.Should().NotContain("MemoryPackIgnoredValue");
        responseContract.Should().NotContain("PasswordHash");
        responseContract.Should().NotContain("AccessToken");
        responseContract.Should().NotContain("BlobBytes");
    }

    [Test]
    public void GenerateService_EmittedResponseSourceHasNoSyntaxErrors()
    {
        var generatedSource = RunGenerator();

        CSharpSyntaxTree.ParseText(generatedSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))
            .GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();
    }

    [Test]
    public void GenerateService_BaseModelMutationsEnforceAndRotateConcurrencyStamp()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("entity.ConcurrencyStamp = Guid.NewGuid();", Exactly.Times(2));
        generatedSource.Should().Contain(
            "UpdateAsync(Guid id, Guid expectedConcurrencyStamp, UpdateWidgetRequest request");
        generatedSource.Should().Contain(
            "DeleteAsync(Guid id, Guid expectedConcurrencyStamp, CancellationToken ct = default)");
        generatedSource.Should().Contain("if (entity.ConcurrencyStamp != expectedConcurrencyStamp)", Exactly.Times(2));
        generatedSource.Should().Contain(
            "Property(e => e.ConcurrencyStamp).OriginalValue = expectedConcurrencyStamp",
            Exactly.Times(2));
        generatedSource.Should().Contain("catch (DbUpdateConcurrencyException ex)", Exactly.Times(2));
        generatedSource.Should().Contain("Result<Widget>.Conflict", Exactly.Times(4));
        generatedSource.Should().Contain("Result.Conflict", Exactly.Times(3));
        generatedSource.Should().Contain("_context.Set<Widget>().AsTracking()", Exactly.Times(2));
    }

    [Test]
    public void GenerateService_CancellationPropagatesAndDatabaseWriteConflictsReturnConflict()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain(
            "catch (OperationCanceledException) when (ct.IsCancellationRequested)",
            Exactly.Times(5));
        generatedSource.Should().Contain("catch (DbUpdateException ex)", Exactly.Times(3));
        generatedSource.Should().Contain("conflicts with an existing record", Exactly.Times(3));
    }

    [Test]
    public void GenerateService_TenantFailuresAreUnauthorizedAndListsAreDeterministic()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("catch (UnauthorizedAccessException ex)", Exactly.Times(5));
        generatedSource.Should().Contain("A valid tenant context is required.", Exactly.Times(5));
        generatedSource.Should().Contain("query = query.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id);");
    }

    [Test]
    public void GenerateService_GlobalReferenceReadsIncludeGlobalAndCurrentTenantRows()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain(
            "((IHasTenantId)e).TenantId == tenantId || ((IHasTenantId)e).TenantId == Guid.Empty",
            Exactly.Times(2));
        generatedSource.Should().Contain(
            "e.Id == id && ((IHasTenantId)e).TenantId == tenantId",
            Exactly.Times(2),
            "mutations must never modify immutable global seed rows");
    }

    [Test]
    public void GenerateService_NonBaseModelListOrdersByIdAndHasNoConcurrencyPrecondition()
    {
        var generatedSource = RunGeneratorForReferencedTenantEntity();

        generatedSource.Should().Contain("query = query.OrderBy(e => e.Id);");
        generatedSource.Should().NotContain("expectedConcurrencyStamp");
        generatedSource.Should().NotContain("DbUpdateConcurrencyException");
    }

    [Test]
    public void GenerateService_DoesNotDeclareUnusedCacheDependency()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().NotContain("ICacheService");
        generatedSource.Should().NotContain("_cacheService");
    }

    [Test]
    public void GenerateService_ExceptionResponsesUseGenericMessages()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("Failure(\"Failed to create Widget.\", 500)");
        generatedSource.Should().Contain("Failure(\"Failed to retrieve Widget.\", 500)");
        generatedSource.Should().Contain("Failure(\"Failed to retrieve Widget list.\", 500)");
        generatedSource.Should().Contain("Failure(\"Failed to update Widget.\", 500)");
        generatedSource.Should().Contain("Failure(\"Failed to delete Widget.\", 500)");
        generatedSource.Should().NotContain("ex.Message");
        generatedSource.Should().NotContain("ex.ToString()");
    }

    [Test]
    public void GenerateService_InjectsTypedLoggerAndLogsEveryCaughtException()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("private readonly ILogger<WidgetService> _logger;");
        generatedSource.Should().Contain("ILogger<WidgetService> logger)");
        generatedSource.Should().Contain("_logger = logger;");
        generatedSource.Should().Contain("catch (Exception ex)", Exactly.Times(5));
        generatedSource.Should().Contain(
            "_logger.LogError(ex, \"Generated entity operation {Operation} failed for {EntityType}\", \"Create\", nameof(Widget));");
        generatedSource.Should().Contain("\"GetById\", nameof(Widget)");
        generatedSource.Should().Contain("\"GetList\", nameof(Widget)");
        generatedSource.Should().Contain("\"Update\", nameof(Widget)");
        generatedSource.Should().Contain("\"Delete\", nameof(Widget)");
        generatedSource.Should().NotContain("Failure($\"");
    }

    [Test]
    public void GenerateService_ListAppliesTypedFiltersAndRejectsUndefinedSearchSemantics()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("if (request.CategoryId.HasValue)");
        generatedSource.Should().Contain("e.CategoryId == request.CategoryId.Value");
        generatedSource.Should().Contain("if (request.OwnerId.HasValue)");
        generatedSource.Should().Contain("e.OwnerId == request.OwnerId");
        generatedSource.Should().Contain("if (!string.IsNullOrWhiteSpace(request.Name))");
        generatedSource.Should().Contain("e.Name == request.Name");
        generatedSource.Should().Contain("if (!string.IsNullOrWhiteSpace(request.SearchTerm))");
        generatedSource.Should().Contain("SearchTerm is not supported for generated Widget lists because searchable fields are not explicitly defined.");
        generatedSource.Should().Contain("Failure(\"SearchTerm is not supported", Exactly.Once());
    }

    [Test]
    public void GenerateService_CreateAndUpdateValidateMappedEntityBeforePersistence()
    {
        var generatedSource = RunGenerator();

        generatedSource.Should().Contain("IEnumerable<IValidator<Widget>> entityValidators");
        generatedSource.Should().Contain("await entityValidator.ValidateAsync(entity, ct)", Exactly.Times(2));

        var createMapping = generatedSource.IndexOf("request.Adapt<Widget>()", StringComparison.Ordinal);
        var createValidation = generatedSource.IndexOf(
            "await entityValidator.ValidateAsync(entity, ct)",
            createMapping,
            StringComparison.Ordinal);
        var createPersistence = generatedSource.IndexOf("_context.Set<Widget>().Add(entity)", StringComparison.Ordinal);
        createMapping.Should().BeLessThan(createValidation);
        createValidation.Should().BeLessThan(createPersistence);

        var updateMapping = generatedSource.IndexOf("request.Adapt(entity)", StringComparison.Ordinal);
        var updateValidation = generatedSource.IndexOf(
            "await entityValidator.ValidateAsync(entity, ct)",
            updateMapping,
            StringComparison.Ordinal);
        var updatePersistence = generatedSource.IndexOf("await _context.SaveChangesAsync(ct)", updateValidation, StringComparison.Ordinal);
        updateMapping.Should().BeLessThan(updateValidation);
        updateValidation.Should().BeLessThan(updatePersistence);
    }

    [Test]
    public void GenerateService_ReferencedTenantEntityAssignsServerTenantBeforeValidation()
    {
        var generatedSource = RunGeneratorForReferencedTenantEntity();

        generatedSource.Should().Contain(
            "private readonly ITrustedInvocationContextAccessor _trustedInvocationContextAccessor;");
        generatedSource.Should().Contain(
            "_trustedInvocationContextAccessor.Current?.EffectiveTenantId");
        generatedSource.Should().NotContain("IHttpContextAccessor");
        generatedSource.Should().NotContain("HttpContext");
        generatedSource.Should().NotContain("FindFirst(");
        generatedSource.Should().Contain("((IHasTenantId)entity).TenantId = tenantId", Exactly.Times(2));

        var createTenantAssignment = generatedSource.IndexOf(
            "((IHasTenantId)entity).TenantId = tenantId",
            StringComparison.Ordinal);
        var createValidation = generatedSource.IndexOf(
            "await entityValidator.ValidateAsync(entity, ct)",
            createTenantAssignment,
            StringComparison.Ordinal);
        createTenantAssignment.Should().BeLessThan(createValidation);

        var updateMapping = generatedSource.IndexOf("request.Adapt(entity)", StringComparison.Ordinal);
        var updateTenantAssignment = generatedSource.IndexOf(
            "((IHasTenantId)entity).TenantId = tenantId",
            updateMapping,
            StringComparison.Ordinal);
        var updateValidation = generatedSource.IndexOf(
            "await entityValidator.ValidateAsync(entity, ct)",
            updateTenantAssignment,
            StringComparison.Ordinal);
        updateMapping.Should().BeLessThan(updateTenantAssignment);
        updateTenantAssignment.Should().BeLessThan(updateValidation);
    }

    [Test]
    public void GenerateService_EmitsAggregateDependencyRegistrations()
    {
        var generatedSource = RunGeneratorTree("GeneratedEntityServiceRegistrations.g.cs");

        generatedSource.Should().Contain(
            "public static IServiceCollection AddGeneratedEntityServices(this IServiceCollection services)");
        generatedSource.Should().Contain(
            "services.AddScoped<global::Sample.IWidgetService, global::Sample.WidgetService>();");
    }

    private static string RunGenerator()
        => RunGeneratorTree("WidgetService.g.cs");

    private static string RunGeneratorTree(string generatedFileName)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "Sample.Api",
            [CSharpSyntaxTree.ParseText(Source, parseOptions)],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new EntityServiceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGenerators(compilation);

        driver.GetRunResult().Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        return driver.GetRunResult()
            .GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith(generatedFileName, StringComparison.Ordinal))
            .GetText()
            .ToString();
    }

    private static string RunGeneratorForReferencedTenantEntity()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var domainCompilation = CSharpCompilation.Create(
            "Sample.Domain.Shared",
            [CSharpSyntaxTree.ParseText(ReferencedDomainSource, parseOptions)],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var domainAssembly = new MemoryStream();
        var emitResult = domainCompilation.Emit(domainAssembly);
        emitResult.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        var apiCompilation = CSharpCompilation.Create(
            "Sample.Api",
            [CSharpSyntaxTree.ParseText("namespace Sample.Api; public sealed class Marker;", parseOptions)],
            GetMetadataReferences().Append(MetadataReference.CreateFromImage(domainAssembly.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new EntityServiceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGenerators(apiCompilation);

        driver.GetRunResult().Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        return driver.GetRunResult()
            .GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("TenantWidgetService.g.cs", StringComparison.Ordinal))
            .GetText()
            .ToString();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .DistinctBy(static reference => reference.Display);

    private const string Source = """
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MemoryPackIgnoreAttribute : Attribute;

namespace XFramework.Core.Attributes
{
    public enum EndpointType
    {
        Service = 1,
        Both = 3
    }

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
    }
}

namespace XFramework.Domain.Shared.Contracts.Base
{
    public interface IHasTenantId
    {
        Guid TenantId { get; set; }
    }

    public interface IAllowsGlobalTenantRows;

    public abstract class BaseModel : IHasTenantId
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}

namespace Sample
{
    [GenerateEndpoints(Type = EndpointType.Both, Actions = EndpointActions.All)]
    public sealed class Widget : XFramework.Domain.Shared.Contracts.Base.BaseModel,
        XFramework.Domain.Shared.Contracts.Base.IAllowsGlobalTenantRows
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid CategoryId { get; set; }
        public Guid? OwnerId { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public RelatedWidget? Owner { get; set; }
        public ICollection<RelatedWidget> Children { get; set; } = [];
        [JsonIgnore] public string? JsonIgnoredValue { get; set; }
        [MemoryPackIgnore] public string? MemoryPackIgnoredValue { get; set; }
        public string? PasswordHash { get; set; }
        public string? AccessToken { get; set; }
        public byte[]? BlobBytes { get; set; }
    }

    public sealed class RelatedWidget;

    public sealed class CreateWidgetRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UpdateWidgetRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class GetWidgetListRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SearchTerm { get; set; }
        public string? Name { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? OwnerId { get; set; }
    }
}
""";

    private const string ReferencedDomainSource = """
using System;
using XFramework.Domain.Shared.Attributes;

namespace XFramework.Domain.Shared.Attributes
{
    public enum EndpointType { Service = 1, Both = 3 }

    [Flags]
    public enum EndpointActions
    {
        Create = 1,
        Get = 2,
        GetList = 4,
        Update = 8,
        Delete = 16,
        All = Create | Get | GetList | Update | Delete
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GenerateEndpointsAttribute : Attribute
    {
        public EndpointType Type { get; set; } = EndpointType.Both;
        public EndpointActions Actions { get; set; } = EndpointActions.All;
    }
}

namespace XFramework.Domain.Shared.Contracts.Base
{
    public interface IHasTenantId
    {
        Guid TenantId { get; set; }
    }
}

namespace Sample.Domain.Shared
{
    [GenerateEndpoints(Type = EndpointType.Both, Actions = EndpointActions.All)]
    public sealed class TenantWidget : XFramework.Domain.Shared.Contracts.Base.IHasTenantId
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CreateTenantWidgetRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UpdateTenantWidgetRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class GetTenantWidgetListRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
""";
}
