using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class PosRegisterFeatureAvailabilityTests
{
    [TestCase(TenantModuleFeatureKeys.InventarioWarehousing, "Inventario Warehousing", true)]
    [TestCase(TenantModuleFeatureKeys.Wallets, "Wallets", true)]
    [TestCase(TenantModuleFeatureKeys.IdentityCredentials, "Identity Credentials", true)]
    [TestCase(null, null, true)]
    [TestCase(TenantModuleFeatureKeys.InventarioWarehousing, "Inventario Warehousing", false)]
    [TestCase(TenantModuleFeatureKeys.Wallets, "Wallets", false)]
    [TestCase(TenantModuleFeatureKeys.IdentityCredentials, "Identity Credentials", false)]
    [TestCase(null, null, false)]
    public async Task Registers_DisabledSetupFeature_DoesNotQueryItOrHideRegisterList(string? disabledFeature, string? warning, bool hasRegister)
    {
        var tenantId = Guid.NewGuid();
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        if (disabledFeature != TenantModuleFeatureKeys.IdentityCredentials) AllowEmptyQuery<IdentityCredential>(data);
        if (disabledFeature != TenantModuleFeatureKeys.Wallets)
        {
            AllowEmptyQuery<Wallet>(data);
            AllowEmptyQuery<WalletType>(data);
            AllowEmptyQuery<CurrencyType>(data);
        }
        if (disabledFeature != TenantModuleFeatureKeys.InventarioWarehousing)
        {
            AllowEmptyQuery<Warehouse>(data);
            AllowEmptyQuery<InventoryLocation>(data);
        }
        var modules = new Mock<IPortalModuleAvailability>();
        modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string module, string? feature) => TenantModuleFeatureKeys.Combine(module, feature) != disabledFeature);
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(tenantId);
        var pos = new Mock<IPOSServiceWrapper>(MockBehavior.Strict);
        pos.Setup(x => x.SearchPosRegisters(It.Is<SearchPosRegistersRequest>(r => r.Metadata.RequestedTenantId == tenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<PosRegisterResponse>>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = hasRegister ? [new() { Id = Guid.NewGuid(), Name = "Existing register", IsEnabled = true }] : []
            });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, StaticJavaScript>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(data.Object);
        services.AddSingleton(modules.Object);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(pos.Object);
        services.AddSingleton(new RequestMetadata());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Registers>()).ToHtmlString());

        if (hasRegister) html.Should().Contain("Existing register");
        else html.Should().Contain("0 register(s) loaded");
        html.Should().NotContain("Registers could not load");
        var create = Regex.Match(html, "<button[^>]*>[^<]*(?:<[^>]+>[^<]*)*Create Register").Value;
        create.Should().NotBeEmpty();
        Regex.IsMatch(create, "\\sdisabled(?:=|\\s|>)").Should().Be(disabledFeature is not null);
        if (warning is not null) html.Should().Contain(warning).And.Contain("Register setup needs additional features");
        else html.Should().NotContain("Register setup needs additional features");
        pos.VerifyAll();
        if (disabledFeature == TenantModuleFeatureKeys.InventarioWarehousing)
        {
            data.Verify(x => x.Query<Warehouse>(), Times.Never);
            data.Verify(x => x.Query<InventoryLocation>(), Times.Never);
        }
        if (disabledFeature == TenantModuleFeatureKeys.Wallets)
        {
            data.Verify(x => x.Query<Wallet>(), Times.Never);
            data.Verify(x => x.Query<WalletType>(), Times.Never);
            data.Verify(x => x.Query<CurrencyType>(), Times.Never);
        }
        if (disabledFeature == TenantModuleFeatureKeys.IdentityCredentials)
            data.Verify(x => x.Query<IdentityCredential>(), Times.Never);
    }

    private static void AllowEmptyQuery<T>(Mock<IDataContext> data) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        data.Setup(x => x.Query<T>()).Returns(query.Object);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/pos/registers");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
