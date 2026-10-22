using System.Net;
using System.Reflection;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Portal.Features.Inventario.Components;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract"), Category("Module:Inventario"), Category("Area:Brands")]
public sealed class ProductBrandPickerTests
{
    [Test]
    public async Task Picker_NoCategory_DoesNotRequestUnscopedSuggestions()
    {
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        using var picker = Picker(wrapper, Tenant(Guid.NewGuid()), null);
        await Invoke(picker, "OnParametersSetAsync");
        Options(picker).Should().BeEmpty();
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Picker_Search_UsesBoundedTenantCategoryScopeAndExplicitNormalizedNewChoice()
    {
        var tenant = Guid.NewGuid();
        var category = Guid.NewGuid();
        var wrapper = new Mock<IInventarioServiceWrapper>();
        wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response("ACME"));
        using var picker = Picker(wrapper, Tenant(tenant), category);
        await Invoke(picker, "OnParametersSetAsync");
        await Invoke(picker, "SearchAsync", " other  brand ");
        wrapper.Verify(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r =>
            r.CategoryId == category && r.Metadata.RequestedTenantId == tenant && r.Limit == 20 && r.Search == "OTHER BRAND"),
            It.IsAny<CancellationToken>()), Times.Once);
        Options(picker).Should().Contain(x => x.Value == "OTHER BRAND" && x.Text.Contains("(new)"));
        picker.AllowNew = false;
        await Invoke(picker, "SearchAsync", " other brand ");
        Options(picker).Should().NotContain(x => x.Value == "OTHER BRAND");
    }

    [Test]
    public async Task Picker_CategoryChange_DiscardsLateSuggestionsFromPreviousCategory()
    {
        var firstCategory = Guid.NewGuid();
        var secondCategory = Guid.NewGuid();
        var pending = new TaskCompletionSource<QueryResponse<List<string>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wrapper = new Mock<IInventarioServiceWrapper>();
        wrapper.Setup(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r => r.CategoryId == firstCategory), It.IsAny<CancellationToken>()))
            .Returns(pending.Task);
        wrapper.Setup(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r => r.CategoryId == secondCategory), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response("SECOND"));
        using var picker = Picker(wrapper, Tenant(Guid.NewGuid()), firstCategory);
        var firstLoad = Invoke(picker, "OnParametersSetAsync");
        picker.CategoryId = secondCategory;
        await Invoke(picker, "OnParametersSetAsync");
        pending.SetResult(Response("FIRST"));
        await firstLoad;
        Options(picker).Select(x => x.Value).Should().Equal("SECOND");
    }

    [Test]
    public async Task Picker_TenantChange_DiscardsLateSuggestionsFromPreviousTenant()
    {
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        var context = Tenant(firstTenant);
        var pending = new TaskCompletionSource<QueryResponse<List<string>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wrapper = new Mock<IInventarioServiceWrapper>();
        wrapper.Setup(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r => r.Metadata.RequestedTenantId == firstTenant), It.IsAny<CancellationToken>()))
            .Returns(pending.Task);
        wrapper.Setup(x => x.SearchProductBrands(It.Is<SearchProductBrandsRequest>(r => r.Metadata.RequestedTenantId == secondTenant), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response("TENANT B"));
        using var picker = Picker(wrapper, context, Guid.NewGuid());
        var firstLoad = Invoke(picker, "OnParametersSetAsync");
        context.SetupGet(x => x.SelectedTenantId).Returns(secondTenant);
        await Invoke(picker, "OnParametersSetAsync");
        pending.SetResult(Response("TENANT A"));
        await firstLoad;
        Options(picker).Select(x => x.Value).Should().Equal("TENANT B");
    }

    [Test]
    public async Task Picker_SearchFailure_DoesNotOfferUnverifiedNewBrand()
    {
        var wrapper = new Mock<IInventarioServiceWrapper>();
        wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<List<string>> { HttpStatusCode = HttpStatusCode.Forbidden });
        using var picker = Picker(wrapper, Tenant(Guid.NewGuid()), Guid.NewGuid());
        await Invoke(picker, "OnParametersSetAsync");
        await Invoke(picker, "SearchAsync", "NEW");
        Options(picker).Should().BeEmpty();
        Field<string?>(picker, "_error").Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Picker_SelectAndClear_NormalizesValue_AndDisabledPickerDoesNotChangeIt()
    {
        var wrapper = new Mock<IInventarioServiceWrapper>();
        wrapper.Setup(x => x.SearchProductBrands(It.IsAny<SearchProductBrandsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response());
        using var picker = Picker(wrapper, Tenant(Guid.NewGuid()), Guid.NewGuid());
        string? selected = "INITIAL";
        picker.ValueChanged = EventCallback.Factory.Create<string?>(new object(), (string? value) => selected = value);
        await Invoke(picker, "OnParametersSetAsync");
        await Invoke(picker, "SelectAsync", " acme  tools ");
        selected.Should().Be("ACME TOOLS");
        picker.Disabled = true;
        await Invoke(picker, "SelectAsync", "");
        selected.Should().Be("ACME TOOLS");
        picker.Disabled = false;
        await Invoke(picker, "SelectAsync", "");
        selected.Should().BeNull();
    }

    [Test]
    public async Task Picker_RenderedWithoutCategory_HasNamedDisabledCombobox()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, NoJavaScript>();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton(Mock.Of<IInventarioServiceWrapper>());
        services.AddSingleton(Tenant(Guid.NewGuid()).Object);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ProductBrandPicker>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ProductBrandPicker.Label)] = "Product brand"
            }))).ToHtmlString());
        html.Should().Contain("aria-label=\"Product brand\"").And.Contain("disabled").And.Contain("Select a category first");
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ProductBrandPicker Picker(Mock<IInventarioServiceWrapper> wrapper, Mock<IPortalTenantContext> tenant, Guid? category)
    {
        var picker = new ProductBrandPicker { CategoryId = category };
        typeof(ProductBrandPicker).GetProperty("Inventario", Private)!.SetValue(picker, wrapper.Object);
        typeof(ProductBrandPicker).GetProperty("TenantContext", Private)!.SetValue(picker, tenant.Object);
        typeof(ProductBrandPicker).GetProperty("Logger", Private)!.SetValue(picker, NullLogger<ProductBrandPicker>.Instance);
        return picker;
    }
    private static Mock<IPortalTenantContext> Tenant(Guid tenant)
    {
        var context = new Mock<IPortalTenantContext>();
        context.SetupGet(x => x.SelectedTenantId).Returns(tenant);
        return context;
    }
    private static QueryResponse<List<string>> Response(params string[] brands) => new() { HttpStatusCode = HttpStatusCode.OK, Response = [.. brands] };
    private static Task Invoke(ProductBrandPicker picker, string method, params object[] arguments) =>
        (Task)typeof(ProductBrandPicker).GetMethod(method, Private)!.Invoke(picker, arguments)!;
    private static T Field<T>(ProductBrandPicker picker, string field) => (T)typeof(ProductBrandPicker).GetField(field, Private)!.GetValue(picker)!;
    private static List<SelectOption<string>> Options(ProductBrandPicker picker) => Field<List<SelectOption<string>>>(picker, "_options");

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => throw new InvalidOperationException("Static rendering must not invoke JavaScript.");
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
}

