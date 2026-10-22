using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using Inventario.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalLayoutBrowser")]
public sealed class InventarioTransactionLayoutTests : PageTest
{
    [TestCase(1920, 1080, false)]
    [TestCase(768, 1024, false)]
    [TestCase(390, 844, false)]
    [TestCase(1920, 1080, true)]
    [TestCase(768, 1024, true)]
    [TestCase(390, 844, true)]
    public async Task Transactions_RenderedComponent_FiltersStayWithinSurface(int width, int height, bool dark)
    {
        var data = new Mock<IPOSServiceWrapper>(MockBehavior.Strict);
        data.Setup(x => x.GetPosProductSalesReport(It.IsAny<GetPosProductSalesReportRequest>(), It.IsAny<CancellationToken>()))
            .Returns((GetPosProductSalesReportRequest request, CancellationToken ct) => Task.FromResult(new QueryResponse<PosProductSalesReportResponse>
            { HttpStatusCode = System.Net.HttpStatusCode.OK, Response = new(Guid.NewGuid(), request.FromUtc, request.ToUtcExclusive, 0, [], []) }));
        var modules = new Mock<IPortalModuleAvailability>();
        modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(Guid.NewGuid());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, StaticJavaScript>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(data.Object);
        services.AddSingleton(new Mock<IInventarioServiceWrapper>(MockBehavior.Strict).Object);
        services.AddSingleton(modules.Object);
        services.AddSingleton(tenant.Object);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var component = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Transactions>()).ToHtmlString());
        var root = FindRepositoryRoot();
        var styles = Path.Combine(TestContext.CurrentContext.TestDirectory, "layout-styles");
        var css = File.ReadAllText(Path.Combine(styles, "themes.css"))
            + File.ReadAllText(Path.Combine(styles, "blazorblueprint.css"))
            + File.ReadAllText(Path.Combine(root, "src", "Presentation", "XFramework.Portal", "wwwroot", "css", "app.css"))
            + File.ReadAllText(Path.Combine(root, "src", "Presentation", "XFramework.Portal", "wwwroot", "css", "utilities.css"))
            + File.ReadAllText(Path.Combine(root, "src", "Presentation", "XFramework.Portal.Features.Inventario", "Pages", "Transactions.razor.css")).Replace("::deep", "");
        // Render the real Razor component and shipped styles, with data mocked and no service credentials.
        var html = $"<!doctype html><html class='{(dark ? "dark" : "")}' data-base-color='slate' data-primary-color='blue' style='--radius:0.5rem'><head>"
            + $"<meta name='viewport' content='width=device-width, initial-scale=1'><style>{css}</style></head>"
            + $"<body style='background:var(--background);color:var(--foreground)'><main style='margin-left:{(width >= 768 ? 288 : 72)}px;padding:24px'>{component}</main></body></html>";
        await Page.SetViewportSizeAsync(width, height);
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Page.SetContentAsync(html);
        var surface = Page.GetByTestId("inventory-transactions-surface");
        var bounds = (await surface.BoundingBoxAsync())!;
        var fields = new List<LocatorBoundingBoxResult>();
        foreach (var id in new[] { "transaction-date-filter" })
        {
            var wrapper = Page.GetByTestId(id);
            var field = (await wrapper.BoundingBoxAsync())!;
            field.Width.Should().BeGreaterThan(100);
            field.X.Should().BeGreaterThanOrEqualTo(bounds.X);
            (field.X + field.Width).Should().BeLessThanOrEqualTo(bounds.X + bounds.Width + 1);
            var control = (await wrapper.Locator("input, button").First.BoundingBoxAsync())!;
            control.X.Should().BeGreaterThanOrEqualTo(field.X);
            (control.X + control.Width).Should().BeLessThanOrEqualTo(field.X + field.Width + 1);
            fields.Add(field);
        }
        for (var i = 0; i < fields.Count; i++)
        for (var j = i + 1; j < fields.Count; j++)
        {
            var a = fields[i]; var b = fields[j];
            (a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y)
                .Should().BeFalse("each filter needs its own space");
        }
        await Expect(Page.GetByLabel("Product search")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sales date range UTC" })).ToBeVisibleAsync();
        (await Page.EvaluateAsync<int>("document.documentElement.scrollWidth")).Should().BeLessThanOrEqualTo(width);
        await Expect(surface.GetByRole(AriaRole.Heading, new() { Name = "Sales Transactions", Exact = true })).ToBeVisibleAsync();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "transaction-layout");
        Directory.CreateDirectory(directory);
        var stem = $"transactions-{width}-{(dark ? "dark" : "light")}";
        await File.WriteAllTextAsync(Path.Combine(directory, stem + ".html"), html);
        var screenshot = Path.Combine(directory, stem + ".png");
        await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        TestContext.AddTestAttachment(screenshot);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XFramework.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/inventario/transactions");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
