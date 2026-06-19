using Wallets.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using XFramework.TestInfrastructure;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Wallets)]
[Category(TestCategories.DataContext)]
public class DataContextTests : WalletsTestBase
{
    [Test]
    public async Task RemoteQuery_ToListAsync_ReturnsWalletTypesFromService()
    {
        var ctx = WalletsTestFixture.DataContext;

        var walletTypes = await ctx.Query<WalletType>()
            .Where(x => x.TenantId == WalletsTestFixture.TestTenantId)
            .ToListAsync();

        walletTypes.Should().Contain(x => x.Id == WalletsTestFixture.TestWalletTypeId);
    }
}
