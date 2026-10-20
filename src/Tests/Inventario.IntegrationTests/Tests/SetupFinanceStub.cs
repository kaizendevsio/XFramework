using System.Net;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;

namespace InventarioOnboarding.IntegrationTests;

internal static class SetupFinanceStub
{
    public static IIdentityServerServiceWrapper Create(params string[] codes)
        => Create(true, codes);

    public static IIdentityServerServiceWrapper Create(bool usable, params string[] codes)
    {
        if (codes.Length == 0) codes = ["PHP", "SGD", "USD"];
        var currencies = codes.Select(code => new TenantFinanceCurrency { Id = Guid.NewGuid(), Code = code, Name = code }).ToList();
        var mock = new Mock<IIdentityServerServiceWrapper>();
        mock.Setup(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTenantFinanceSettingsRequest request, CancellationToken _) => new QueryResponse<TenantFinanceSettingsResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new()
                {
                    TenantId = request.Metadata.RequestedTenantId!.Value,
                    IsUsable = usable,
                    Settings = new() { DefaultCurrencyId = currencies[0].Id, SupportedCurrencyIds = currencies.Select(x => x.Id).ToList() },
                    SupportedCurrencies = currencies
                }
            });
        return mock.Object;
    }
}
