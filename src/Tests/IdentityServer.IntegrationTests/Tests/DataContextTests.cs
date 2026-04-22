using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;

namespace IdentityServer.IntegrationTests;

/// <summary>
/// End-to-end tests for RemoteDataContext: test client -> Bolt Hub -> IdentityServer -> DbContext -> PostgreSQL.
/// Verifies that IDataContext queries sent from the test client are routed through Bolt
/// to the IdentityServer service, executed against the real database, and returned.
/// </summary>
[TestFixture]
public class DataContextTests
{
    [Test]
    public async Task RemoteQuery_ToListAsync_ReturnsEntitiesFromService()
    {
        var ctx = IntegrationTestFixture.DataContext;

        var results = await ctx.Query<Tenant>()
            .Where(t => t.Id == IntegrationTestFixture.TestTenantId)
            .ToListAsync();

        results.Should().NotBeEmpty();
        results[0].Id.Should().Be(IntegrationTestFixture.TestTenantId);
    }

    [Test]
    public async Task RemoteQuery_FirstOrDefaultAsync_ReturnsSingleEntity()
    {
        var ctx = IntegrationTestFixture.DataContext;

        var result = await ctx.Query<Tenant>()
            .Where(t => t.Id == IntegrationTestFixture.TestTenantId)
            .FirstOrDefaultAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(IntegrationTestFixture.TestTenantId);
    }

    [Test]
    public async Task RemoteQuery_CountAsync_ReturnsCount()
    {
        var ctx = IntegrationTestFixture.DataContext;

        var count = await ctx.Query<Tenant>()
            .Where(t => t.Id == IntegrationTestFixture.TestTenantId)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RemoteQuery_AnyAsync_ReturnsTrue()
    {
        var ctx = IntegrationTestFixture.DataContext;

        var exists = await ctx.Query<Tenant>()
            .AnyAsync();

        exists.Should().BeTrue();
    }
}
