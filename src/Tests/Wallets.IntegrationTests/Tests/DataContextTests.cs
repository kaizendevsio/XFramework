using Wallets.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Net;
using XFramework.TestInfrastructure;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Wallets)]
[Category(TestCategories.DataContext)]
public class DataContextTests : WalletsTestBase
{
    [Test]
    public async Task GeneratedWalletTypeRead_WithCapability_SucceedsAcrossAllGeneratedPaths()
    {
        var credential = await TestHelpers.SeedCredentialWithRole(
            WalletsTestFixture.ConnectionString);
        HttpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthHeaders.CredentialId,
            credential.Id.ToString());
        HttpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthHeaders.IdentityId,
            credential.IdentityInfoId.ToString());
        var restResponse = await HttpClient.GetAsync($"/api/wallet-types/{WalletsTestFixture.TestWalletTypeId}");
        restResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await restResponse.Content.ReadAsStringAsync());

        using var actor = WalletsTestFixture.PushActor(credential.Id);
        var wrapperResponse = await WalletsTestFixture.ServiceWrapper.WalletType.Get(
            WalletsTestFixture.TestWalletTypeId);
        wrapperResponse.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        wrapperResponse.Response.Should().NotBeNull();

        var walletType = await WalletsTestFixture.DataContext.Query<WalletType>()
            .Where(x => x.Id == WalletsTestFixture.TestWalletTypeId)
            .FirstOrDefaultAsync();
        walletType.Should().NotBeNull();
    }

    [Test]
    public async Task GeneratedWalletTypeRead_WithoutCapability_IsForbiddenAcrossAllGeneratedPaths()
    {
        HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Wallets-Test-No-Role", "true");
        var restResponse = await HttpClient.GetAsync($"/api/wallet-types/{WalletsTestFixture.TestWalletTypeId}");
        restResponse.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            await restResponse.Content.ReadAsStringAsync());

        using var actor = WalletsTestFixture.PushActor(Guid.NewGuid(), privileged: false);
        var wrapperResponse = await WalletsTestFixture.ServiceWrapper.WalletType.Get(
            WalletsTestFixture.TestWalletTypeId);
        wrapperResponse.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);

        var query = async () => await WalletsTestFixture.DataContext.Query<WalletType>()
            .Where(x => x.Id == WalletsTestFixture.TestWalletTypeId)
            .FirstOrDefaultAsync();
        await query.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status 403*");
    }

    [Test]
    public async Task RemoteQuery_WithoutWalletCapability_IsForbidden()
    {
        using var actor = WalletsTestFixture.PushActor(Guid.NewGuid(), privileged: false);

        var query = async () => await WalletsTestFixture.DataContext.Query<WalletType>()
            .Where(x => x.TenantId == WalletsTestFixture.TestTenantId)
            .ToListAsync();

        await query.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status 403*");
    }

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
