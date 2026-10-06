using System.Linq.Expressions;
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
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
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
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        EmptyQuery<Product>(data);
        EmptyQuery<ProductTransaction>(data);
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
        services.AddSingleton(modules.Object);
        services.AddSingleton(tenant.Object);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var component = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Transactions>()).ToHtmlString());
        var root = FindRepositoryRoot();
        var package = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", "blazorblueprint.components", "3.12.0", "staticwebassets");
        var css = File.ReadAllText(Path.Combine(package, "css", "themes.css"))
            + File.ReadAllText(Path.Combine(package, "blazorblueprint.css"))
            + File.ReadAllText(Path.Combine(root, "src", "Presentation", "XFramework.Portal", "wwwroot", "css", "app.css"));
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
        foreach (var id in new[] { "transaction-product-filter", "transaction-from-filter", "transaction-through-filter" })
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
        await Expect(Page.GetByLabel("Product search")).ToBeVisibleAsync();
        (await Page.EvaluateAsync<int>("document.documentElement.scrollWidth")).Should().BeLessThanOrEqualTo(width);
        await Expect(surface.GetByRole(AriaRole.Heading, new() { Name = "No sales transactions found", Exact = true })).ToBeVisibleAsync();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "transaction-layout");
        Directory.CreateDirectory(directory);
        var stem = $"transactions-{width}-{(dark ? "dark" : "light")}";
        await File.WriteAllTextAsync(Path.Combine(directory, stem + ".html"), html);
        var screenshot = Path.Combine(directory, stem + ".png");
        await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        TestContext.AddTestAttachment(screenshot);
    }

    private static void EmptyQuery<T>(Mock<IDataContext> data) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.OrderByDescending(It.IsAny<Expression<Func<T, DateTime>>>())).Returns(query.Object);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        data.Setup(x => x.Query<T>()).Returns(query.Object);
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
