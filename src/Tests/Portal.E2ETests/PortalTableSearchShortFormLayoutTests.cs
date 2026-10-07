using System.Linq.Expressions;
using System.Reflection;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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
public sealed class PortalTableSearchShortFormLayoutTests : PageTest
{
    [TestCase(1920, 1080, false)]
    [TestCase(768, 1024, false)]
    [TestCase(390, 844, false)]
    [TestCase(1920, 1080, true)]
    [TestCase(768, 1024, true)]
    [TestCase(390, 844, true)]
    public async Task TablesAndForms_RenderedComponents_StayWithinResponsiveWidths(int width, int height, bool dark)
    {
        await using var provider = CreateServices().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var categories = await Render<Categories>(renderer);
        var customSearch = await Render<BbFormFieldInput<string>>(renderer, new()
        {
            ["Label"] = "Community search", ["Class"] = "xf-table-search"
        });
        var fullSearch = await Render<BbInput>(renderer, new() { ["AriaLabel"] = "Product search", ["Class"] = "pos-search-input" });
        var field = await Render<BbFormFieldInput<string>>(renderer, new() { ["Label"] = "Long form field" });
        var html = $"<main style='padding:24px'><section id='categories'>{categories}</section>"
            + $"<section id='custom-search'>{customSearch}</section><section id='product-search'>{fullSearch}</section>"
            + "<section class='xf-entity-picker-advanced-dialog' style='width:100%;max-width:none'><input aria-label='Advanced search' class='w-full'></section>"
            + $"<section id='long-form' class='grid gap-3 md:grid-cols-2'>{field}{field}</section></main>";
        await LoadStyles(html, width, height, dark);

        var surface = Page.Locator("#categories");
        var search = surface.GetByPlaceholder("Search categories...");
        await Expect(search).ToBeVisibleAsync();
        var searchBox = (await search.BoundingBoxAsync())!;
        var surfaceBox = (await surface.Locator(".inv-page").BoundingBoxAsync())!;
        searchBox.Width.Should().BeLessThanOrEqualTo(385);
        (searchBox.X + searchBox.Width).Should().BeLessThanOrEqualTo(surfaceBox.X + surfaceBox.Width + 1);
        if (width >= 768) searchBox.Width.Should().BeApproximately(384, 1);
        else searchBox.Width.Should().BeGreaterThan(200);
        (await Page.GetByLabel("Community search").BoundingBoxAsync())!.Width.Should().BeApproximately(Math.Min(384, width - 48), 1);
        foreach (var name in new[] { "Product search", "Advanced search" })
            (await Page.GetByLabel(name).BoundingBoxAsync())!.Width.Should().BeApproximately(width - 48, 1);

        var shortForm = Page.Locator("[role='dialog'] .xf-short-form");
        await Expect(shortForm).ToBeVisibleAsync();
        var inputs = shortForm.Locator("input");
        (await inputs.CountAsync()).Should().Be(2);
        var first = (await inputs.Nth(0).BoundingBoxAsync())!;
        var second = (await inputs.Nth(1).BoundingBoxAsync())!;
        first.X.Should().BeApproximately(second.X, 1);
        first.Width.Should().BeApproximately(second.Width, 1);
        second.Y.Should().BeGreaterThanOrEqualTo(first.Y + first.Height);
        var longInputs = Page.Locator("#long-form input");
        var longFirst = (await longInputs.Nth(0).BoundingBoxAsync())!;
        var longSecond = (await longInputs.Nth(1).BoundingBoxAsync())!;
        if (width >= 768) longFirst.Y.Should().BeApproximately(longSecond.Y, 1);
        else longSecond.Y.Should().BeGreaterThan(longFirst.Y);
        await Capture($"tables-forms-{width}-{(dark ? "dark" : "light")}");
    }

    [TestCase(1920, 1080)]
    [TestCase(390, 844)]
    [TestCase(390, 400)]
    public async Task PaymentDialog_TallContent_FitsViewportWithOneScrollbar(int width, int height)
    {
        await using var provider = CreateServices().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var field = await Render<BbFormFieldInput<string>>(renderer, new() { ["Label"] = "Payment field" });
        RenderFragment body = builder => builder.AddMarkupContent(0,
            $"<div class='pos-checkout-panel'>{string.Concat(Enumerable.Repeat(field, 20))}</div>");
        RenderFragment content = builder =>
        {
            builder.OpenComponent<BbDialogContent>(0);
            builder.AddAttribute(1, "Class", "pos-payment-dialog");
            builder.AddAttribute(2, "ChildContent", body);
            builder.CloseComponent();
        };
        var dialogHtml = await Render<BbDialog>(renderer, new() { ["Open"] = true, ["ChildContent"] = content });
        await LoadStyles(dialogHtml, width, height, false);
        var dialog = Page.Locator(".pos-payment-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        var bounds = (await dialog.BoundingBoxAsync())!;
        bounds.Width.Should().BeApproximately(Math.Min(512, width - 32), 1);
        bounds.Height.Should().BeLessThanOrEqualTo(height - 32 + 1);
        bounds.X.Should().BeGreaterThanOrEqualTo(15);
        bounds.Y.Should().BeGreaterThanOrEqualTo(15);
        (bounds.Y + bounds.Height).Should().BeLessThanOrEqualTo(height - 15);
        (await dialog.EvaluateAsync<string>("el => getComputedStyle(el).overflowY")).Should().Be("auto");
        (await dialog.EvaluateAsync<bool>("el => el.scrollHeight > el.clientHeight")).Should().BeTrue();
        var panel = dialog.Locator(".pos-checkout-panel");
        (await panel.EvaluateAsync<string>("el => getComputedStyle(el).borderTopWidth")).Should().Be("0px");
        (await panel.EvaluateAsync<string>("el => getComputedStyle(el).overflowY")).Should().Be("visible");
        await dialog.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
        await Expect(panel.Locator("input").Last).ToBeInViewportAsync();
        await Capture($"payment-dialog-{width}-{height}");
    }

    private static ServiceCollection CreateServices()
    {
        var data = new Mock<IDataContext>(MockBehavior.Strict);
        Query(data, Enumerable.Range(1, 25).Select(i => new ProductCategory
        {
            Id = Guid.NewGuid(), Name = $"Category {i}", Description = "Catalog category"
        }).ToList());
        Query<Product>(data, []);
        var modules = new Mock<IPortalModuleAvailability>();
        modules.Setup(x => x.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        modules.Setup(x => x.IsFeatureEnabled(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(Guid.NewGuid());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, StaticJavaScript>();
        services.AddSingleton<IComponentActivator, OpenCategoryDialogActivator>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(data.Object);
        services.AddSingleton(modules.Object);
        services.AddSingleton(tenant.Object);
        return services;
    }

    private static void Query<T>(Mock<IDataContext> data, List<T> rows) where T : class
    {
        var query = new Mock<IRemoteQuery<T>>(MockBehavior.Strict);
        query.Setup(x => x.IgnoreQueryFilters()).Returns(query.Object);
        query.Setup(x => x.NoCache()).Returns(query.Object);
        query.Setup(x => x.Where(It.IsAny<Expression<Func<T, bool>>>())).Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<T, string>>>())).Returns(query.Object);
        query.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        data.Setup(x => x.Query<T>()).Returns(query.Object);
    }

    private static Task<string> Render<T>(HtmlRenderer renderer, Dictionary<string, object?>? parameters = null) where T : IComponent =>
        renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>(
            ParameterView.FromDictionary(parameters ?? []))).ToHtmlString());

    private async Task LoadStyles(string body, int width, int height, bool dark)
    {
        var root = new DirectoryInfo(Environment.GetEnvironmentVariable("XFRAMEWORK_TEST_REPOSITORY_ROOT")
            ?? TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "XFramework.slnx"))) root = root.Parent;
        var styles = Path.Combine(TestContext.CurrentContext.TestDirectory, "layout-styles");
        var css = File.ReadAllText(Path.Combine(styles, "themes.css"))
            + File.ReadAllText(Path.Combine(styles, "blazorblueprint.css"))
            + File.ReadAllText(Path.Combine(root!.FullName, "src/Presentation/XFramework.Portal/wwwroot/css/app.css"));
        await Page.SetViewportSizeAsync(width, height);
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Page.SetContentAsync($"<!doctype html><html class='{(dark ? "dark" : "")}' data-base-color='slate' data-primary-color='blue' style='--radius:0.5rem'>"
            + $"<head><meta name='viewport' content='width=device-width, initial-scale=1'><style>{css}</style></head>"
            + $"<body style='background:var(--background);color:var(--foreground)'>{body}</body></html>");
    }

    private async Task Capture(string name)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "portal-table-search-short-form");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), await Page.ContentAsync());
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path);
    }

    // HtmlRenderer cannot run the JS portal lifecycle. Keep the pinned dialog's classes and real child components.
    public sealed class InlineDialogContent : BbDialogContent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var classes = typeof(BbDialogContent).GetMethod("GetClassNames", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null);
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "role", "dialog");
            builder.AddAttribute(2, "class", classes);
            builder.AddContent(3, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class OpenCategoryDialogActivator : IComponentActivator
    {
        public IComponent CreateInstance(Type type)
        {
            if (type == typeof(BbDialogContent)) return new InlineDialogContent();
            var component = (IComponent)Activator.CreateInstance(type)!;
            if (component is Categories)
                type.GetField("_dialogOpen", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, true);
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/inventario/categories");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
