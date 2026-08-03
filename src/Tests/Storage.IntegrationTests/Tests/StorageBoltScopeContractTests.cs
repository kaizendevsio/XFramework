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
        "GetStorageFileEndpoint",
        "GetStorageFilesEndpoint",
        "GetStoragePublicUrlEndpoint",
        "GetStorageDownloadUrlEndpoint",
        "ValidateStorageFileReferenceEndpoint",
        "ListStorageUploadPartsEndpoint"
    ];

    [Test]
    public void EveryStorageBoltHandler_RequiresItsOperationScope()
    {
        var handlers = typeof(GetStorageFileEndpoint).Assembly.GetTypes()
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
            handler.Attribute!.RequiredServiceScopes.Should().Equal(expectedScope);
        }
    }
}
