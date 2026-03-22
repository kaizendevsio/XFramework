using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Contexts;

namespace IdentityServer.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase
{
    protected HttpClient HttpClient { get; private set; } = null!;

    [SetUp]
    public void BaseSetUp()
    {
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(IntegrationTestFixture.IdentityServerUrl)
        };
    }

    [TearDown]
    public void BaseTearDown()
    {
        HttpClient?.Dispose();
    }

    /// <summary>
    /// Gets a fresh DbContext for direct database assertions.
    /// Uses IConfiguration with Tenant:DefaultId for proper tenant query filter resolution.
    /// </summary>
    protected AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(IntegrationTestFixture.ConnectionString)
            .Options;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = IntegrationTestFixture.TestTenantId.ToString()
            })
            .Build();

        return new AppDbContext(options, new HttpContextAccessor(), config);
    }

    protected static string UniqueUsername() => $"testuser_{Guid.NewGuid():N}";
    protected static string UniqueEmail() => $"test_{Guid.NewGuid():N}@test.com";
    protected static string UniquePhone() => $"+1{Random.Shared.Next(1000000000, 1999999999)}";
}
