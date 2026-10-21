using System.Net;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class TenantPosSettingsWrapperTests : IntegrationTestBase
{
    [Test]
    public async Task PosSettings_ThroughWrapper_RoundTripsTargetTenantAndRejectsStaleSave()
    {
        var name = $"POS wrapper {Guid.NewGuid():N}";
        var created = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = name, Version = 1m, Status = 1,
            ParentTenantId = IntegrationTestFixture.TestTenantId, Metadata = Metadata()
        });
        created.IsSuccess.Should().BeTrue(created.Message);
        await using var db = CreateDbContext();
        var tenant = await db.Set<Tenant>().IgnoreQueryFilters().SingleAsync(x => x.Name == name);

        var initial = await IntegrationTestFixture.ServiceWrapper.GetTenantPosSettings(
            new GetTenantPosSettingsRequest { Metadata = Metadata(tenant.Id) });
        initial.HttpStatusCode.Should().Be(HttpStatusCode.OK, initial.Message);
        initial.Response!.TenantId.Should().Be(tenant.Id);
        initial.Response.IsUsable.Should().BeTrue();
        initial.Response.Settings.TableTrackingEnabled.Should().BeFalse();

        var request = new UpdateTenantPosSettingsRequest
        {
            Metadata = Metadata(tenant.Id), ExpectedConcurrencyStamp = initial.Response.ConcurrencyStamp,
            Settings = new PosCheckoutSettings
            {
                TableTrackingEnabled = true,
                CheckoutFields = [new() { Key = "coupon", Label = "Coupon", Enabled = true, Required = true }]
            }
        };
        var saved = await IntegrationTestFixture.ServiceWrapper.UpdateTenantPosSettings(request);
        saved.HttpStatusCode.Should().Be(HttpStatusCode.OK, saved.Message);
        saved.Response!.ConcurrencyStamp.Should().NotBeEmpty();
        saved.Response.TenantId.Should().Be(tenant.Id);
        var read = await IntegrationTestFixture.ServiceWrapper.GetTenantPosSettings(
            new GetTenantPosSettingsRequest { Metadata = Metadata(tenant.Id) });
        read.IsSuccess.Should().BeTrue(read.Message);
        read.Response!.Settings.Should().BeEquivalentTo(request.Settings);
        read.Response.ConcurrencyStamp.Should().Be(saved.Response.ConcurrencyStamp);

        var stale = await IntegrationTestFixture.ServiceWrapper.UpdateTenantPosSettings(request);
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict, stale.Message);
        var rows = await db.Set<RegistryConfiguration>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && x.Key == PosCheckoutSettings.RegistryKey).ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ConcurrencyStamp.Should().Be(saved.Response.ConcurrencyStamp);
    }

    [Test]
    public async Task GetTenantPosSettings_WithoutActor_IsRejected()
    {
        using var actor = IntegrationTestFixture.SuppressActorAccessToken();
        var result = await IntegrationTestFixture.ServiceWrapper.GetTenantPosSettings(
            new GetTenantPosSettingsRequest { Metadata = Metadata(IntegrationTestFixture.TestTenantId) });
        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    private static RequestMetadata Metadata(Guid? tenantId = null) => new()
    {
        RequestedTenantId = tenantId, RequestId = Guid.NewGuid(),
        OperationName = nameof(TenantPosSettingsWrapperTests), IpAddress = "127.0.0.1",
        DeviceName = "TestDevice", UserAgent = "TestAgent"
    };
}
