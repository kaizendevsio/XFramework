using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using System.Text.RegularExpressions;
using XFramework.Portal.Features.Inventario;

namespace Portal.E2ETests;

[TestFixture, NonParallelizable]
[Category("Area:PortalLayoutBrowser")]
public sealed class InventarioFilterLayoutTests : PageTest
{
    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    [TestCase(390, 844)]
    public async Task StockFilters_HaveReadableWidthsAndAlignedActions(int width, int height)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, StaticJavaScript>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        RenderFragment controls = builder =>
        {
            builder.OpenComponent<BbFormFieldInput<string>>(0);
            builder.AddAttribute(1, "Label", "Search stock");
            builder.AddAttribute(2, "Placeholder", "Product, SKU, variant or lot");
            builder.CloseComponent();
            builder.OpenComponent<InventorySelect<string>>(3);
            builder.AddAttribute(4, "Label", "Warehouse");
            builder.AddAttribute(5, "Value", "all");
            builder.AddAttribute(6, "DisplayTextSelector", (Func<string, string>)(_ => "Main E2E Warehouse"));
            builder.CloseComponent();
            builder.OpenComponent<BbButton>(7);
            builder.AddAttribute(8, "Variant", ButtonVariant.Outline);
            builder.AddAttribute(9, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Clear filters")));
            builder.CloseComponent();
        };
        var markup = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<InventoryFilterBar>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Label"] = "Stock filters", ["ChildContent"] = controls }))).ToHtmlString());
        var root = Root();
        var styles = Path.Combine(TestContext.CurrentContext.TestDirectory, "layout-styles");
        var css = File.ReadAllText(Path.Combine(styles, "themes.css"))
            + File.ReadAllText(Path.Combine(styles, "blazorblueprint.css"))
            + File.ReadAllText(Path.Combine(root, "src/Presentation/XFramework.Portal/wwwroot/css/app.css"))
            + File.ReadAllText(Path.Combine(root, "src/Presentation/XFramework.Portal/wwwroot/css/utilities.css"))
            + File.ReadAllText(Path.Combine(root, "src/Presentation/XFramework.Portal.Features.Inventario/obj/Debug/net10.0/scopedcss/projectbundle/XFramework.Portal.Features.Inventario.bundle.scp.css"));
        await Page.SetViewportSizeAsync(width, height);
        var appSource = File.ReadAllText(Path.Combine(root, "src/Presentation/XFramework.Portal/Components/App.razor"));
        var themeScript = Regex.Match(appSource, "<script>.*?</script>", RegexOptions.Singleline).Value;
        themeScript.Should().Contain("bb-theme");
        await Page.SetContentAsync($"<html><head><style>{css}</style>{themeScript}</head>"
            + $"<body class='font-sans antialiased'><main class='inventario-shell' style='margin-left:{(width >= 768 ? 256 : 0)}px;padding:24px'>{markup}</main></body></html>");
        foreach (var control in new[] { Page.GetByLabel("Search stock", new() { Exact = true }), Page.GetByRole(AriaRole.Combobox, new() { Name = "Warehouse" }) })
        {
            (await control.EvaluateAsync<double>("e => parseFloat(getComputedStyle(e).borderTopLeftRadius)")).Should().BeGreaterThan(0, "Blueprint controls must have the Portal theme applied");
            (await control.EvaluateAsync<double>("e => parseFloat(getComputedStyle(e).paddingLeft)")).Should().BeGreaterThan(0, "Blueprint component styles must be loaded");
        }
        var search = (await Page.GetByLabel("Search stock", new() { Exact = true }).BoundingBoxAsync())!;
        var warehouse = (await Page.GetByRole(AriaRole.Combobox, new() { Name = "Warehouse" }).BoundingBoxAsync())!;
        var clear = (await Page.GetByRole(AriaRole.Button, new() { Name = "Clear filters" }).BoundingBoxAsync())!;
        warehouse.Width.Should().BeGreaterThanOrEqualTo(200);
        search.Width.Should().BeGreaterThanOrEqualTo(200);
        search.Width.Should().BeLessThanOrEqualTo(384);
        warehouse.Width.Should().BeLessThanOrEqualTo(384);
        clear.Width.Should().BeGreaterThanOrEqualTo(100);
        if (width == 1920)
        {
            Math.Abs(search.X - 280).Should().BeLessThan(2, "filters should start at the content edge");
            Math.Abs(search.Y + search.Height - clear.Y - clear.Height).Should().BeLessThan(2);
            Math.Abs(warehouse.Y + warehouse.Height - clear.Y - clear.Height).Should().BeLessThan(2);
        }
        (await Page.EvaluateAsync<int>("document.documentElement.scrollWidth")).Should().BeLessThanOrEqualTo(width);
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "inventory-polish");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"stock-filters-{width}.png");
        await Page.ScreenshotAsync(new() { Path = path });
        TestContext.AddTestAttachment(path);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XFramework.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => throw new InvalidOperationException();
    }
}
