using Microsoft.EntityFrameworkCore;
using Npgsql;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.TestInfrastructure;

namespace Inventario.IntegrationTests.Infrastructure;

public abstract class InventarioTestBase
{
    protected HttpClient HttpClient { get; private set; } = null!;

    [SetUp]
    public void BaseSetUp()
    {
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(InventarioIntegrationTestFixture.InventarioUrl)
        };
    }

    [TearDown]
    public void BaseTearDown() => HttpClient.Dispose();

    protected AppDbContext CreateDbContext()
    {
        var connectionString = InventarioIntegrationTestFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Inventario integration test database connection string was not initialized.");
        }

        var connection = new NpgsqlConnection(connectionString);
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(connection)
            .Options;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = InventarioIntegrationTestFixture.TestTenantId.ToString()
            })
            .Build();

        return new AppDbContext(options, new HttpContextAccessor(), config);
    }

    protected static RequestMetadata CreateMetadata() => new()
    {
        TenantId = InventarioIntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "InventarioIntegrationTest",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };

    protected static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, prefix.Length + 33)];
}
