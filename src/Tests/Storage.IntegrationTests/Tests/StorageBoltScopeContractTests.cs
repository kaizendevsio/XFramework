using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Storage.Api.Features.Files.Get;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.TestInfrastructure;

namespace Storage.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Storage)]
[Category(TestCategories.Auth)]
public sealed class StorageBoltScopeContractTests
{
    private static readonly HashSet<string> ReadHandlers =
    [
        "GetStorageFileMetadataEndpoint",
        "GetStorageFilesEndpoint",
        "GetStoragePublicUrlEndpoint",
        "GetStorageDownloadUrlEndpoint",
        "ValidateStorageFileReferenceEndpoint",
        "ListStorageUploadPartsEndpoint"
    ];

    private static readonly HashSet<string> ServiceTargetWriteHandlers =
    [
        "ClaimStorageFileEndpoint",
        "DeleteStorageFileEndpoint"
    ];

    [Test]
    public void EveryStorageBoltHandler_RequiresItsOperationScope()
    {
        var handlers = typeof(GetStorageFileMetadataEndpoint).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Storage.Api.Features", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(method => (Type: type, Attribute: method.GetCustomAttribute<BoltHandlerAttribute>())))
            .Where(item => item.Attribute is not null)
            .ToList();

        handlers.Should().HaveCount(15);
        foreach (var handler in handlers)
        {
            var expectedScope = ReadHandlers.Contains(handler.Type.Name)
                ? XFrameworkServiceScopes.StorageRead
                : XFrameworkServiceScopes.StorageWrite;
            var expectedScopes = ServiceTargetWriteHandlers.Contains(handler.Type.Name)
                ? new[] { expectedScope, XFrameworkServiceScopes.TenantTarget }
                : [expectedScope];
            handler.Attribute!.RequiredServiceScopes.Should().Equal(expectedScopes);
        }
    }

    [Test]
    public void UploadPart_AuthorizesBeforeBufferingRequestBody()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "src",
            "Modules",
            "XFramework.Storage",
            "Storage.Api",
            "Features",
            "Sessions",
            "UploadPart",
            "Endpoint.cs"));

        var authorizationIndex = source.IndexOf("invocationAuthorizer.AuthorizeAsync(", StringComparison.Ordinal);
        var boundedBodyReadIndex = source.IndexOf("ReadBoundedBodyAsync(httpRequest.Body", StringComparison.Ordinal);

        authorizationIndex.Should().BeGreaterThanOrEqualTo(0);
        boundedBodyReadIndex.Should().BeGreaterThan(authorizationIndex,
            "unauthorized callers must be rejected before their upload body is buffered");
        source.Should().Contain("if (totalBytes > MaxUploadPartBytes)",
            "chunked request bodies must be bounded even when Content-Length is absent");
        source.Should().NotContain("httpRequest.ContentLength",
            "client-controlled Content-Length must not bypass the authoritative bounded body reader");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
                return directory;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
