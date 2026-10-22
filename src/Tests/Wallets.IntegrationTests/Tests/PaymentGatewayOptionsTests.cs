using System.Net;
using System.Net.Http.Json;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;
using SharedContracts = XFramework.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
public sealed class PaymentGatewayOptionsTests : WalletsTestBase
{
    [Test]
    public async Task Wrapper_Options_FiltersTenantAndStateBeforePagination_ProjectsDisplayFields()
    {
        var tenantId = WalletsTestFixture.TestTenantId;
        var otherTenantId = Guid.NewGuid();
        var category = Category(tenantId);
        var otherCategory = Category(otherTenantId);
        var first = Gateway(category, "QA gateway A");
        var second = Gateway(category, "QA gateway A");
        var disabled = Gateway(category, "QA disabled", enabled: false);
        var deleted = Gateway(category, "QA deleted", deleted: true);
        var foreign = Gateway(otherCategory, "QA other tenant");
        await using var db = CreateDbContext();
        db.Add(category);
        db.AddRange(first, second, disabled, deleted);
        await db.SaveChangesAsync();
        // The legacy gateway mapping uses a true database default for inserted booleans.
        await db.Set<SharedContracts.PaymentGateway>()
            .Where(x => x.Id == disabled.Id && x.TenantId == tenantId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsEnabled, false));
        await using (var otherDb = CreateDbContext(otherTenantId))
        {
            otherDb.AddRange(otherCategory, foreign);
            await otherDb.SaveChangesAsync();
        }

        var expectedIds = await db.Set<SharedContracts.PaymentGateway>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IsEnabled && !x.IsDeleted)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => x.Id).ToListAsync();
        var token = TestInvocationIdentityExtensions.CreateTestActorToken(
            otherTenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [],
            [WalletAuthorizationCapabilities.ReportingView, XFrameworkActorCapabilities.IdentityTenantsManage]);
        using var actor = TestInvocationActorTokenScope.Push(token);
        var actual = new List<WalletPaymentGatewayOptionResponse>();
        for (var page = 1; page <= (expectedIds.Count + 1) / 2 + 1; page++)
        {
            var response = await WalletsTestFixture.ServiceWrapper.WalletPaymentGatewayOptions(new()
            {
                Page = page, PageSize = 2, Metadata = CreateMetadata()
            });
            response.IsSuccess.Should().BeTrue(response.Message);
            response.Response.Should().HaveCountLessThanOrEqualTo(2);
            actual.AddRange(response.Response!);
        }

        actual.Select(x => x.Id).Should().Equal(expectedIds);
        actual.Should().NotContain(x => x.Id == disabled.Id || x.Id == deleted.Id || x.Id == foreign.Id);
        actual.Single(x => x.Id == first.Id).Should().BeEquivalentTo(new WalletPaymentGatewayOptionResponse
        {
            Id = first.Id, Name = first.Name, Description = first.Description,
            GatewayCategoryId = category.Id, CategoryName = category.Name,
            ServiceCharge = first.ServiceCharge, ConvenienceFee = first.ConvenienceFee, Discount = first.Discount
        });
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task Wrapper_Options_DelegatedTenant_RequiresReportingAndTenantManagement(bool canReport, bool canManageTenants)
    {
        List<string> capabilities = [];
        if (canReport) capabilities.Add(WalletAuthorizationCapabilities.ReportingView);
        if (canManageTenants) capabilities.Add(XFrameworkActorCapabilities.IdentityTenantsManage);
        var token = TestInvocationIdentityExtensions.CreateTestActorToken(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], capabilities);
        using var actor = TestInvocationActorTokenScope.Push(token);
        var response = await WalletsTestFixture.ServiceWrapper.WalletPaymentGatewayOptions(new()
        {
            Metadata = CreateMetadata()
        });

        response.IsSuccess.Should().Be(canReport && canManageTenants, response.Message);
        if (!canReport || !canManageTenants)
            response.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Http_Options_SameTenant_RequiresReportingCapability(bool canReport)
    {
        HttpClient.DefaultRequestHeaders.Add("X-Wallets-Test-No-Role", "true");
        HttpClient.DefaultRequestHeaders.Add("X-Wallets-Test-Capabilities",
            canReport ? WalletAuthorizationCapabilities.ReportingView : WalletAuthorizationCapabilities.View);
        var response = await HttpClient.PostAsJsonAsync("/api/wallets/payment-gateway-options",
            new WalletPaymentGatewayOptionsRequest { Metadata = CreateMetadata() });

        response.StatusCode.Should().Be(canReport ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            await response.Content.ReadAsStringAsync());
        if (canReport)
            (await response.Content.ReadFromJsonAsync<List<WalletPaymentGatewayOptionResponse>>()).Should().NotBeNull();
    }

    [Test]
    public async Task Wrapper_Options_DisabledRootFeature_DeniesRead()
    {
        try
        {
            await SetWalletsEnabled(false);
            var response = await WalletsTestFixture.ServiceWrapper.WalletPaymentGatewayOptions(new()
            {
                Metadata = CreateMetadata()
            });
            response.IsSuccess.Should().BeFalse();
            response.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await SetWalletsEnabled(true);
        }
    }

    private async Task SetWalletsEnabled(bool enabled)
    {
        await using var db = CreateDbContext();
        var feature = await db.Set<TenantModuleFeature>().IgnoreQueryFilters().SingleAsync(x =>
            x.TenantId == WalletsTestFixture.TestTenantId &&
            x.ModuleKey == TenantModuleFeatureKeys.Wallets && x.SubFeatureKey == string.Empty);
        feature.IsEnabled = enabled;
        await db.SaveChangesAsync();
        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantModuleFeatureService>()
            .Invalidate(WalletsTestFixture.TestTenantId, TenantModuleFeatureKeys.Wallets, null);
    }

    private static SharedContracts.PaymentGatewayCategory Category(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = "QA category",
        Description = "Gateway options integration test", IsEnabled = true
    };

    private static SharedContracts.PaymentGateway Gateway(
        SharedContracts.PaymentGatewayCategory category, string name, bool enabled = true, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = category.TenantId, GatewayCategoryId = category.Id,
        Name = name, Description = "Gateway options integration test", IsEnabled = enabled, IsDeleted = deleted,
        ServiceCharge = 1.25m, ConvenienceFee = 2.50m, Discount = 0.50m
    };
}
