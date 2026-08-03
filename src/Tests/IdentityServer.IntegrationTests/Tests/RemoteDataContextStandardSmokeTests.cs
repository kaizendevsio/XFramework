using IdentityServer.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.DataContext)]
public sealed class RemoteDataContextStandardSmokeTests
{
    [Test]
    public async Task RemoteQuery_CurrentTenant_RoundTripsThroughBolt()
    {
        var tenant = await IntegrationTestFixture.DataContext.Query<Tenant>()
            .Where(item => item.Id == IntegrationTestFixture.TestTenantId)
            .FirstOrDefaultAsync();

        tenant.Should().NotBeNull();
        tenant!.Id.Should().Be(IntegrationTestFixture.TestTenantId);
    }
}
