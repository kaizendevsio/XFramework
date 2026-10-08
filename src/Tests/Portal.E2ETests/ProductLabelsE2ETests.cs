using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Portal.Features.Inventario.Components;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:Inventario")]
public sealed class ProductLabelsE2ETests : PageTest
{
    private WebApplication app = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            [WebHostDefaults.StaticWebAssetsKey] = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json")
        });
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(x => x.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        app = builder.Build();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapRazorComponents<ProductLabelsFixtureRoot>().AddInteractiveServerRenderMode();
        await app.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { await app.StopAsync(); await app.DisposeAsync(); }

    [TearDown]
    public async Task Diagnostics()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            TestContext.Progress.WriteLine(await Page.Locator("body").InnerTextAsync());
    }

    [TestCase(390,844)]
    [TestCase(1920,1080)]
    public async Task Labels_SelectPreviewDecode_DownloadAndPrintPdf(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/product-labels-fixture");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Print labels", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByText("Products without a valid SKU cannot be printed.", new() { Exact = false })).ToBeVisibleAsync();
        var downloadButton = Page.GetByRole(AriaRole.Button, new() { Name = "Download PDF", Exact = true });
        await Expect(downloadButton).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Coffee beans" }).GetByRole(AriaRole.Checkbox).ClickAsync();
        await Expect(Page.GetByAltText("Product QR preview")).ToBeVisibleAsync();
        var decoded = await Page.GetByAltText("Product QR preview").EvaluateAsync<string>("""
            async image => {
                await image.decode();
                const barcode = await import('/_content/XFramework.Portal.Shared/barcodes/barcodes.js');
                const canvas = document.createElement('canvas'); canvas.width = image.naturalWidth; canvas.height = image.naturalHeight;
                const ctx = canvas.getContext('2d'); ctx.drawImage(image,0,0);
                return (await barcode.decode(ctx.getImageData(0,0,canvas.width,canvas.height)))[0].text;
            }
            """);
        decoded.Should().Be("COFFEE-001");
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "product-labels");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"labels-{width}.png"), FullPage = true });
        (await Page.GetByRole(AriaRole.Dialog).EvaluateAsync<bool>("e => { const r=e.getBoundingClientRect();return r.left>=0 && r.right<=innerWidth; }")).Should().BeTrue();
        var download = await Page.RunAndWaitForDownloadAsync(() => downloadButton.ClickAsync());
        download.SuggestedFilename.Should().Be("Product-QR-labels.pdf");
        var path = Path.Combine(directory, $"labels-a4-{width}.pdf");
        await download.SaveAsAsync(path);
        new FileInfo(path).Length.Should().BeGreaterThan(1000);
        var printed = new TaskCompletionSource<IDownload>(TaskCreationOptions.RunContinuationsAsynchronously);
        void CapturePopup(object? _, IPage page) => page.Download += (_, download) => printed.TrySetResult(download);
        Page.Context.Page += CapturePopup;
        var popup = await Page.RunAndWaitForPopupAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync());
        // Headless Chromium downloads native PDFs instead of hosting its desktop print viewer.
        var printable = await printed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await printable.SaveAsAsync(Path.Combine(directory, $"labels-print-{width}.pdf"));
        Page.Context.Page -= CapturePopup;
        await popup.CloseAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Print labels", Exact = true }).ClickAsync();
        await Expect(downloadButton).ToBeDisabledAsync();
    }

    [Test]
    public async Task Pdf_StickerDimensions_MultiPageAndInvalidInput()
    {
        await Page.GotoAsync(app.Urls.Single() + "/product-labels-fixture");
        var metrics = await Page.EvaluateAsync<double[]>("""
            async () => {
                const labels = await import('/_content/XFramework.Portal.Features.Inventario/labels/product-labels.js');
                const model = { products:[{name:'Coffee beans',sku:'COFFEE-001'}], copies:3,width:60,height:40,sheet:'label' };
                const pdf = await labels.createLabelPdf(model);
                let denied = 0;
                for(const patch of [{copies:0},{copies:501},{width:0},{products:[{name:'Missing',sku:''}]}]) {
                    try { await labels.createLabelPdf({...model,...patch}); } catch { denied++; }
                }
                const a4 = await labels.createLabelPdf({...model,sheet:'a4',copies:20});
                return [pdf.internal.pageSize.getWidth(),pdf.internal.pageSize.getHeight(),pdf.getNumberOfPages(),a4.getNumberOfPages(),denied];
            }
            """);
        metrics[0].Should().BeApproximately(60,.01); metrics[1].Should().BeApproximately(40,.01);
        metrics[2].Should().Be(3); metrics[3].Should().Be(2); metrics[4].Should().Be(4);
        var download = await Page.RunAndWaitForDownloadAsync(async () => await Page.EvaluateAsync("""
            async () => (await import('/_content/XFramework.Portal.Features.Inventario/labels/product-labels.js')).downloadPdf({
                products:[{name:'Coffee beans - dark roast with a long product name',sku:'COFFEE-001'}],copies:2,width:60,height:40,sheet:'label'
            })
            """));
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "product-labels");
        Directory.CreateDirectory(directory);
        await download.SaveAsAsync(Path.Combine(directory, "labels-sticker.pdf"));
    }
}

[Route("/product-labels-fixture")]
public sealed class ProductLabelsFixtureRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0,"<!doctype html><html data-base-color='zinc'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='XFramework.Portal.styles.css'><link rel='stylesheet' href='css/app.css'></head><body>");
        b.OpenComponent<ProductLabelsFixtureSurface>(1); b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender:false)); b.CloseComponent();
        b.AddMarkupContent(2,"<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class ProductLabelsFixtureSurface : ComponentBase
{
    private readonly Guid tenantId = Guid.NewGuid();
    private List<Product> products = [];
    private bool open;
    protected override void OnInitialized() => products = [new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Coffee beans", SKU = "COFFEE-001" }, new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Missing SKU" }];
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<BbButton>(0); b.AddAttribute(1,"OnClick",EventCallback.Factory.Create<MouseEventArgs>(this,_=>open=true)); b.AddAttribute(2,"ChildContent",(RenderFragment)(c=>c.AddContent(0,"Print labels"))); b.CloseComponent();
        b.OpenComponent<ProductLabelsDialog>(3); b.AddAttribute(4,"Open",open); b.AddAttribute(5,"OpenChanged",EventCallback.Factory.Create<bool>(this,value=>open=value)); b.AddAttribute(6,"TenantId",tenantId); b.AddAttribute(7,"Products",products); b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(8); b.CloseComponent(); b.OpenComponent<BbOverlayPortalHost>(9); b.CloseComponent();
    }
}
