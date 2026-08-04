using Storage.Integration.Drivers;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using NUnit.Framework;

namespace Storage.IntegrationTests;

public abstract class StorageIntegrationTestBase
{
    protected HttpClient HttpClient { get; private set; } = null!;

    protected static IStorageServiceWrapper ServiceWrapper =>
        StorageIntegrationTestFixture.ServiceWrapper;

    protected static IDataContext DataContext =>
        StorageIntegrationTestFixture.DataContext;

    [SetUp]
    public void BaseSetUp()
    {
        StorageIntegrationTestFixture.Provider.Reset();
        HttpClient = new HttpClient { BaseAddress = new Uri(StorageIntegrationTestFixture.StorageUrl) };
    }

    [TearDown]
    public void BaseTearDown() => HttpClient?.Dispose();

    protected static AppDbContext CreateDbContext() =>
        StorageIntegrationTestFixture.CreateDbContext();

    protected static RequestMetadata CreateMetadata(Guid? tenantId = null) => new()
    {
        RequestedTenantId = tenantId ?? StorageIntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "StorageIntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };
}
