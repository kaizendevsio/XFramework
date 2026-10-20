using System.Net;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Moq;
using POS.Api.Services;
using XFramework.Domain.Shared.BusinessObjects;

namespace POS.IntegrationTests;

[TestFixture]
public sealed class PosFinanceSettingsTests
{
    [TestCase(true, true, true, 200)]
    [TestCase(false, true, true, 403)]
    [TestCase(true, false, true, 409)]
    [TestCase(true, true, false, 409)]
    public async Task GetAsync_ValidatesTenantAndSupportedCurrency(bool sameTenant, bool supportedCurrency, bool usable, int status)
    {
        var tenant = Guid.NewGuid();
        var currency = Guid.NewGuid();
        var wrapper = new Mock<IIdentityServerServiceWrapper>();
        wrapper.Setup(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<TenantFinanceSettingsResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new() { IsUsable = usable, TenantId = sameTenant ? tenant : Guid.NewGuid(), Settings = new() { DefaultCurrencyId = currency, SupportedCurrencyIds = [currency] } }
            });
        var result = await new PosFinanceSettings(wrapper.Object).GetAsync(new(), tenant,
            supportedCurrency ? currency : Guid.NewGuid(), CancellationToken.None);
        result.StatusCode.Should().Be(status);
    }

    [Test]
    public async Task GetAsync_UnavailableSettings_DoesNotSilentlyDisableTax()
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>();
        wrapper.Setup(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<TenantFinanceSettingsResponse> { HttpStatusCode = HttpStatusCode.ServiceUnavailable });
        var result = await new PosFinanceSettings(wrapper.Object).GetAsync(new(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        result.StatusCode.Should().Be(503);
        result.IsSuccess.Should().BeFalse();
    }
}
