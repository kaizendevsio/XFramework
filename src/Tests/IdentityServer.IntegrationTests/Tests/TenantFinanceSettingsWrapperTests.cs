using System.Net;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.TestInfrastructure;
using CurrencyType = Wallets.Domain.Shared.Contracts.CurrencyType;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class TenantFinanceSettingsWrapperTests : IntegrationTestBase
{
    [Test]
    public async Task FinanceSettings_ThroughWrapper_RoundTripsTargetTenantAndRejectsStaleSave()
    {
        var name = $"Finance wrapper {Guid.NewGuid():N}";
        var created = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = name, Version = 1m, Status = 1,
            ParentTenantId = IntegrationTestFixture.TestTenantId, Metadata = Metadata()
        });
        created.IsSuccess.Should().BeTrue(created.Message);

        await using var db = CreateDbContext();
        var tenant = await db.Set<Tenant>().IgnoreQueryFilters().SingleAsync(x => x.Name == name);
        // Isolated fixture reference data; production currency writes remain owned by Wallets.
        var currency = new CurrencyType
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, CurrencyIsoCode3 = "USD", Name = "US Dollar",
            SystemReferenceId = Guid.NewGuid(), IsEnabled = true, ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(currency);
        await db.SaveChangesAsync();

        var initial = await IntegrationTestFixture.ServiceWrapper.GetTenantFinanceSettings(
            new GetTenantFinanceSettingsRequest { Metadata = Metadata(tenant.Id) });
        initial.HttpStatusCode.Should().Be(HttpStatusCode.OK, initial.Message);
        initial.Response!.TenantId.Should().Be(tenant.Id);
        initial.Response.IsUsable.Should().BeTrue();
        initial.Response.AvailableCurrencies.Should().Contain(x => x.Id == currency.Id);

        var request = new UpdateTenantFinanceSettingsRequest
        {
            Metadata = Metadata(tenant.Id), ExpectedConcurrencyStamp = initial.Response.ConcurrencyStamp,
            Settings = new TenantFinanceSettings
            {
                DefaultCurrencyId = currency.Id, SupportedCurrencyIds = [currency.Id],
                TaxEnabled = true, TaxRatePercent = 9m
            }
        };
        var saved = await IntegrationTestFixture.ServiceWrapper.UpdateTenantFinanceSettings(request);
        saved.HttpStatusCode.Should().Be(HttpStatusCode.OK, saved.Message);
        saved.Response!.ConcurrencyStamp.Should().NotBeEmpty();
        saved.Response.TenantId.Should().Be(tenant.Id);

        var read = await IntegrationTestFixture.ServiceWrapper.GetTenantFinanceSettings(
            new GetTenantFinanceSettingsRequest { Metadata = Metadata(tenant.Id) });
        read.IsSuccess.Should().BeTrue(read.Message);
        read.Response!.Settings.TaxRatePercent.Should().Be(9m);
        read.Response.Settings.DefaultCurrencyId.Should().Be(currency.Id);
        read.Response.ConcurrencyStamp.Should().Be(saved.Response.ConcurrencyStamp);

        var stale = await IntegrationTestFixture.ServiceWrapper.UpdateTenantFinanceSettings(request);
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict, stale.Message);
        var rows = await db.Set<RegistryConfiguration>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && x.Key == TenantFinanceSettings.RegistryKey).ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ConcurrencyStamp.Should().Be(saved.Response.ConcurrencyStamp);
    }

    [Test]
    public async Task GetTenantFinanceSettings_WithoutActor_IsRejected()
    {
        using var actor = IntegrationTestFixture.SuppressActorAccessToken();
        var result = await IntegrationTestFixture.ServiceWrapper.GetTenantFinanceSettings(
            new GetTenantFinanceSettingsRequest { Metadata = Metadata(IntegrationTestFixture.TestTenantId) });
        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    private static RequestMetadata Metadata(Guid? tenantId = null) => new()
    {
        RequestedTenantId = tenantId, RequestId = Guid.NewGuid(),
        OperationName = nameof(TenantFinanceSettingsWrapperTests), IpAddress = "127.0.0.1",
        DeviceName = "TestDevice", UserAgent = "TestAgent"
    };
}
