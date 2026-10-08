using System.Security.Claims;
using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using POS.Integration.Drivers;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Features.POS.Scanner;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

// Isolated static-render/camera harness. Never starts Portal with deployed credentials.
[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:POS")]
[Category("Area:Scanner")]
public sealed class PosScannerCameraE2ETests : PageTest
{
    private WebApplication app = null!;
    private string url = "";

    [OneTimeSetUp]
    public async Task StartFixture()
    {
        var root = RepoRoot();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddLogging();
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddSingleton<IJSRuntime, StaticJavaScript>();
        builder.Services.AddSingleton(Mock.Of<IPOSServiceWrapper>());
        builder.Services.AddSingleton(Mock.Of<IPortalActorContext>());
        builder.Services.AddSingleton<AuthenticationStateProvider, FixtureAuthentication>();
        app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(root, "src/Presentation/XFramework.Portal.Shared/wwwroot")),
            RequestPath = "/_content/XFramework.Portal.Shared"
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(root, "src/Presentation/XFramework.Portal.Features.POS/wwwroot")),
            RequestPath = "/_content/XFramework.Portal.Features.POS"
        });
        app.MapGet("/fixture", async () =>
        {
            await using var scope = app.Services.CreateAsyncScope();
            await using var renderer = new HtmlRenderer(scope.ServiceProvider,
                scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<MobileScanner>()).ToHtmlString());
            var css = await File.ReadAllTextAsync(Path.Combine(root,
                "src/Presentation/XFramework.Portal.Features.POS/Pages/MobileScanner.razor.css"));
            return Microsoft.AspNetCore.Http.Results.Content(
                "<!doctype html><html><head><meta name='viewport' content='width=device-width,initial-scale=1'>" +
                "<style>body{margin:0;font-family:Arial;color:#222;background:#fff}main{max-width:528px;margin:auto;padding:16px}button{font:inherit;padding:12px;border:1px solid #ddd;border-radius:6px}input{height:42px;width:100%;box-sizing:border-box}svg{vertical-align:middle}label{font-size:14px}" +
                css + "</style></head><body>" + html +
                "<script type='module'>window.scanner=await import('/_content/XFramework.Portal.Features.POS/scanner/scanner.js')</script></body></html>",
                "text/html");
        });
        await app.StartAsync();
        url = app.Urls.Single();
    }

    [OneTimeTearDown]
    public async Task StopFixture() { if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); } }

    [SetUp]
    public async Task LoadFixture()
    {
        await Page.GotoAsync(url + "/fixture");
        await Page.WaitForFunctionAsync("!!window.scanner");
    }

    [TestCase("QRCode", "SKU-QR-1")]
    [TestCase("EAN13", "5901234123457")]
    [TestCase("EAN8", "96385074")]
    [TestCase("UPCA", "012345678905")]
    [TestCase("Code128", "SKU-128-1")]
    [TestCase("Code39", "SKU-39")]
    [TestCase("ITF", "123456789012")]
    [TestCase("DataMatrix", "SKU-DM-1")]
    public async Task Decoder_RealGeneratedBarcode_RoundTripsWithoutNetworkDecoder(string format, string code)
    {
        var result = await Page.EvaluateAsync<string>("""
            async ({format,code}) => {
                const library = await scanner.decoder();
                const output = await library.writeBarcode(code,{format,scale:5});
                const bitmap = await createImageBitmap(output.image);
                const canvas = document.createElement('canvas');
                canvas.width=bitmap.width;canvas.height=bitmap.height;
                const context=canvas.getContext('2d');
                context.drawImage(bitmap,0,0);
                const decoded=await scanner.decode(context.getImageData(0,0,canvas.width,canvas.height));
                return decoded.find(r=>r.isValid)?.text ?? '';
            }
            """, new { format, code });
        result.Should().Be(format == "UPCA" ? "0" + code : code,
            "ZXing normalizes EAN/UPC output to EAN-13; do not guess alternate SKUs");
    }

    [Test]
    public async Task Camera_DeterministicVideoFixture_DebouncesAndStopsTracks()
    {
        await Page.EvaluateAsync("""
            async () => {
                const library=await scanner.decoder();
                const output=await library.writeBarcode('SKU-CAMERA',{format:'QRCode',scale:8});
                const bitmap=window.fixtureBitmap=await createImageBitmap(output.image);
                window.fixtureCanvas=document.createElement('canvas');
                fixtureCanvas.width=640;fixtureCanvas.height=480;
                const c=fixtureCanvas.getContext('2d');c.fillStyle='white';c.fillRect(0,0,640,480);
                c.drawImage(bitmap,100,40);
                window.fixtureStream=fixtureCanvas.captureStream(10);
                navigator.mediaDevices.getUserMedia=async()=>fixtureStream;
                window.detected=[];
                await scanner.start(document.querySelector('video'), {
                    invokeMethodAsync:async(name,text)=>{if(name==='Decoded') detected.push(text); return true;}
                });
            }
            """);
        await Page.WaitForFunctionAsync("detected.length===1");
        await Page.WaitForTimeoutAsync(1000);
        (await Page.EvaluateAsync<string[]>("detected")).Should().Equal("SKU-CAMERA");
        await Page.EvaluateAsync("(()=>{const c=fixtureCanvas.getContext('2d');c.fillStyle='white';c.fillRect(0,0,640,480)})()");
        await Page.WaitForTimeoutAsync(1400);
        await Page.EvaluateAsync("fixtureCanvas.getContext('2d').drawImage(fixtureBitmap,100,40)");
        await Page.WaitForFunctionAsync("detected.length===2");
        (await Page.EvaluateAsync<string[]>("detected")).Should().Equal("SKU-CAMERA", "SKU-CAMERA");
        await Page.EvaluateAsync("scanner.stop(document.querySelector('video'))");
        (await Page.EvaluateAsync<bool>("fixtureStream.getTracks().every(t=>t.readyState==='ended')")).Should().BeTrue();
        (await Page.EvaluateAsync<bool>("document.querySelector('video').srcObject===null")).Should().BeTrue();
    }

    [Test]
    public async Task Camera_PermissionDenied_ReturnsMeaningfulError()
    {
        var error = await Page.EvaluateAsync<string>("""
            async () => {
                navigator.mediaDevices.getUserMedia=async()=>{throw new DOMException('denied','NotAllowedError')};
                try { await scanner.start(document.querySelector('video'),{}); return ''; }
                catch(e){return e.message}
            }
            """);
        error.Should().Contain("permission was denied");
        (await Page.EvaluateAsync<bool>("document.querySelector('video').srcObject===null")).Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Camera_CancelDuringDecoderLoad_NeverRequestsOrActivatesCamera(bool pageExit)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.RouteAsync("**/vendor/zxing-wasm-3.1.5.js",async route =>
        { entered.TrySetResult();await release.Task;await route.ContinueAsync(); });
        try
        {
            await Page.EvaluateAsync("""
                () => {
                    window.mediaRequests=0;
                    navigator.mediaDevices.getUserMedia=async()=>{mediaRequests++;throw new Error('Must not request camera after cancel')};
                    window.pendingStart=scanner.start(document.querySelector('video'),{invokeMethodAsync:async()=>true});
                }
                """);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Page.EvaluateAsync(pageExit ? "window.dispatchEvent(new Event('pagehide'))" : "scanner.stop(document.querySelector('video'))");
            release.TrySetResult();
            (await Page.EvaluateAsync<bool>("async()=>await pendingStart")).Should().BeFalse();
            (await Page.EvaluateAsync<int>("mediaRequests")).Should().Be(0);
            (await Page.EvaluateAsync<bool>("document.querySelector('video').srcObject===null")).Should().BeTrue();
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    public async Task Camera_StopDuringPermissionAwait_StopsLateTracksWithoutActivatingPreview()
    {
        await Page.EvaluateAsync("""
            () => {
                const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;
                window.fixtureStream=canvas.captureStream(10);
                window.mediaRequests=0;
                navigator.mediaDevices.getUserMedia=()=>{mediaRequests++;return new Promise(resolve=>window.resolveMedia=resolve)};
                window.pendingStart=scanner.start(document.querySelector('video'),{invokeMethodAsync:async()=>true});
            }
            """);
        await Page.WaitForFunctionAsync("mediaRequests===1");
        await Page.EvaluateAsync("scanner.stop(document.querySelector('video'));resolveMedia(fixtureStream)");
        (await Page.EvaluateAsync<bool>("async()=>await pendingStart")).Should().BeFalse();
        (await Page.EvaluateAsync<bool>("fixtureStream.getTracks().every(t=>t.readyState==='ended')")).Should().BeTrue();
        (await Page.EvaluateAsync<bool>("document.querySelector('video').srcObject===null")).Should().BeTrue();
    }

    [Test]
    public async Task Camera_OldCancelledStart_CannotStopReplacementAfterSharedDecoderLoad()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.RouteAsync("**/vendor/zxing-wasm-3.1.5.js",async route =>
        { entered.TrySetResult();await release.Task;await route.ContinueAsync(); });
        try
        {
            await Page.EvaluateAsync("""
                () => {
                    const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;
                    const context=canvas.getContext('2d');context.fillStyle='white';context.fillRect(0,0,640,480);
                    window.fixtureStream=canvas.captureStream(10);window.mediaRequests=0;
                    window.fixtureFrames=setInterval(()=>context.fillRect(0,0,640,480),100);
                    navigator.mediaDevices.getUserMedia=async()=>{mediaRequests++;return fixtureStream};
                    window.receiver={invokeMethodAsync:async()=>true};
                    window.oldStart=scanner.start(document.querySelector('video'),receiver);
                }
                """);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Page.EvaluateAsync("()=>{scanner.stop(document.querySelector('video'));window.newStart=scanner.start(document.querySelector('video'),receiver)}");
            release.TrySetResult();
            (await Page.EvaluateAsync<bool>("async()=>await oldStart")).Should().BeFalse();
            (await Page.EvaluateAsync<bool>("async()=>await newStart").WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
            (await Page.EvaluateAsync<int>("mediaRequests")).Should().Be(1);
            (await Page.EvaluateAsync<bool>("fixtureStream.getTracks().every(t=>t.readyState==='live')")).Should().BeTrue();
            await Page.EvaluateAsync("scanner.stop(document.querySelector('video'))");
            await Page.EvaluateAsync("clearInterval(fixtureFrames)");
            (await Page.EvaluateAsync<bool>("fixtureStream.getTracks().every(t=>t.readyState==='ended')")).Should().BeTrue();
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    public async Task PairingChallenge_OnlyOwnSecureOriginQr_AndFragmentIsRemoved()
    {
        var challenge = new string('A', 64);
        var result = await Page.EvaluateAsync<string[]>("""
            ({challenge}) => {
                const own=location.origin+'/pos/mobile-scanner#'+challenge;
                const foreign='https://untrusted.invalid/pos/mobile-scanner#'+challenge;
                history.replaceState(null,'',location.pathname+'#'+challenge);
                const captured=scanner.takeChallenge();
                return [scanner.pairingChallenge(own),scanner.pairingChallenge(foreign),captured,location.hash];
            }
            """, new { challenge });
        result.Should().Equal(challenge, "", challenge, "");
        var tenant = Guid.NewGuid().ToString();
        (await Page.EvaluateAsync<string>("tenant=>scanner.pairingTenant(location.origin+'/pos/mobile-scanner?tenant='+tenant)", tenant)).Should().Be(tenant);
        (await Page.EvaluateAsync<string>("tenant=>scanner.pairingTenant('https://foreign.invalid/pos/mobile-scanner?tenant='+tenant)", tenant)).Should().BeEmpty();
    }

    [TestCase(390, 844)]
    [TestCase(1366, 768)]
    public async Task Scanner_RenderedMobileSurface_IsContainedAndAccessible(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Pair scanner" })).ToBeVisibleAsync();
        var pairingCode = Page.GetByLabel("Six-digit pairing code", new() { Exact = true });
        await Expect(pairingCode).ToBeVisibleAsync();
        await Expect(pairingCode).ToHaveAttributeAsync("inputmode", "numeric");
        await Expect(pairingCode).ToHaveAttributeAsync("maxlength", "6");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Start camera" })).ToBeVisibleAsync();
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth")).Should().BeTrue();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "pos-scanner");
        Directory.CreateDirectory(directory);
        var image = Path.Combine(directory, $"scanner-{width}.png");
        await Page.ScreenshotAsync(new() { Path = image, FullPage = true });
        TestContext.AddTestAttachment(image, "Static scanner surface; no deployed auth or physical camera");
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "src/Presentation/XFramework.Portal.Features.POS/wwwroot")))
                return directory.FullName;
        throw new InvalidOperationException("Scanner E2E fixture requires repository assets");
    }

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => throw new InvalidOperationException("Static render cannot invoke JS");
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object?[]? args) => InvokeAsync<T>(id, args);
    }

    private sealed class FixtureAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Fixture cashier")], "fixture"))));
    }
}
