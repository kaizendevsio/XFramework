using System.Net;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Portal.Features.POS.Pages;
using Wallets.Domain.Shared.Contracts;
using XFramework.Portal.Features.POS.Scanner;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;
using System.Security.Claims;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:POS")]
[Category("Area:Scanner")]
public sealed class PosScannerPairingDialogE2ETests : PageTest
{
    private WebApplication app = null!;
    private PairingFixtureState state = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        var root = RepoRoot();
        var assetsManifest = Path.Combine(TestContext.CurrentContext.TestDirectory, "XFramework.Portal.staticwebassets.runtime.json");
        File.Exists(assetsManifest).Should().BeTrue("the referenced Portal build supplies its runtime asset manifest");
        state = new();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Portal:ScannerPublicBaseUrl"] = "https://scanner.fixture.invalid",
            [WebHostDefaults.StaticWebAssetsKey] = assetsManifest
        });
        // Use the framework and package assets selected by this Portal build, including SDK patch versions.
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(options => options.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(state);
        builder.Services.AddScoped<AuthenticationStateProvider, PosScannerFixtureAuthentication>();
        builder.Services.AddSingleton(Mock.Of<IPortalTenantContext>(t => t.SelectedTenantId == state.TenantId));
        builder.Services.AddSingleton(Mock.Of<IDataContext>());
        builder.Services.AddSingleton(Mock.Of<IPortalModuleAvailability>());
        builder.Services.AddSingleton(Mock.Of<IPortalActorContext>(a => a.CredentialId == state.CashierId));
        builder.Services.AddScoped(_ => new RequestMetadata());
        var wrapper = new Mock<IPOSServiceWrapper>();
        wrapper.Setup(w => w.CreatePosScannerPairing(It.IsAny<CreatePosScannerPairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                state.LatestPairing = new(Guid.NewGuid(), new string('B',64), new string('A',64),
                    DateTimeOffset.UtcNow.AddSeconds(state.ExpirySeconds),
                    Interlocked.Increment(ref state.NextPairingCode).ToString("D6",System.Globalization.CultureInfo.InvariantCulture));
                state.ActivePairings.TryAdd(state.LatestPairing.PairingId,0);
                return new CmdResponse<PosScannerPairingResponse> { HttpStatusCode = HttpStatusCode.OK, Response = state.LatestPairing };
            });
        wrapper.Setup(w => w.PollPosScannerCodes(It.IsAny<PollPosScannerCodesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PollPosScannerCodesRequest request, CancellationToken _) =>
            {
                if (!state.ActivePairings.ContainsKey(request.PairingId))
                    return new QueryResponse<PosScannerPollResponse> { HttpStatusCode = HttpStatusCode.Forbidden };
                if (!state.Paired && state.LatestPairing is { } pairing && pairing.PairingId == request.PairingId &&
                    pairing.ChallengeExpiresAt <= DateTimeOffset.UtcNow)
                    return new QueryResponse<PosScannerPollResponse> { HttpStatusCode = HttpStatusCode.Forbidden };
                Interlocked.Increment(ref state.Polls);
                if (request.PauseDelivery) Interlocked.Increment(ref state.PausedPolls);
                if (!request.PauseDelivery) Interlocked.Exchange(ref state.Acknowledged, request.AcknowledgedSequence);
                var queued = state.QueuedScan;
                if (request.DiscardPendingCodes)
                {
                    var acknowledged = queued?.Sequence ?? request.AcknowledgedSequence;
                    state.QueuedScan = null;
                    Interlocked.Exchange(ref state.Acknowledged, acknowledged);
                    return new QueryResponse<PosScannerPollResponse>
                    { HttpStatusCode = HttpStatusCode.OK, Response = new(state.Paired, DateTimeOffset.UtcNow.AddMinutes(30), [], acknowledged) };
                }
                var codes = new List<PosScannerCodeResponse>();
                if (!request.PauseDelivery)
                {
                    if (queued is not null && queued.PairingId == request.PairingId && request.AcknowledgedSequence < queued.Sequence)
                        codes.Add(new(queued.Sequence,queued.Code));
                    else if (state.Pending && request.AcknowledgedSequence == 0)
                        codes.Add(new(1,"SKU-1"));
                }
                return new QueryResponse<PosScannerPollResponse>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = new(state.Paired, DateTimeOffset.UtcNow.AddMinutes(30),
                        codes)
                };
            });
        wrapper.Setup(w => w.ClaimPosScannerPairing(It.IsAny<ClaimPosScannerPairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimPosScannerPairingRequest request, CancellationToken _) =>
            {
                state.Claims.Enqueue(request);
                if (request.PairingCode != "000007" && request.Challenge != new string('A',64))
                    return new CmdResponse<PosScannerPhoneResponse> { HttpStatusCode = HttpStatusCode.Forbidden, Message = "Pairing denied." };
                return new CmdResponse<PosScannerPhoneResponse>
                { HttpStatusCode = HttpStatusCode.OK, Response = new(Guid.NewGuid(), new string('C',64), "Fixture register", DateTimeOffset.UtcNow.AddMinutes(30)) };
            });
        wrapper.Setup(w => w.RevokePosScannerPairing(It.IsAny<RevokePosScannerPairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RevokePosScannerPairingRequest request,CancellationToken _) =>
            {
                if (state.ActivePairings.TryRemove(request.PairingId,out var removed))
                { state.RevokedPairings.Enqueue(request.PairingId); Interlocked.Increment(ref state.Revokes); }
                return new CmdResponse<bool> { HttpStatusCode = HttpStatusCode.OK, Response = true };
            });
        wrapper.Setup(w => w.GetPosScannerStatus(It.IsAny<GetPosScannerStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetPosScannerStatusRequest request, CancellationToken _) => new QueryResponse<PosScannerPhoneResponse>
            { HttpStatusCode = HttpStatusCode.OK, Response = new(request.PairingId, request.PhoneKey, "Fixture register", DateTimeOffset.UtcNow.AddHours(12)) });
        wrapper.Setup(w => w.SendPosScannerCode(It.IsAny<SendPosScannerCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendPosScannerCodeRequest request, CancellationToken _) => new CmdResponse<PosScannerSendResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new(request.Sequence, false) });
        wrapper.Setup(w => w.SearchPosCatalog(It.IsAny<SearchPosCatalogRequest>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchPosCatalogRequest request,CancellationToken _) =>
            {
                state.CatalogRequests.Enqueue(request);
                return new QueryResponse<List<PosCatalogItemResponse>>
                { HttpStatusCode = HttpStatusCode.OK, Response = [state.Item] };
            });
        wrapper.Setup(w => w.CheckoutPosSale(It.IsAny<CheckoutPosSaleRequest>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref state.FinancialCalls);
                return new CmdResponse<PosSaleReceiptResponse> { HttpStatusCode = HttpStatusCode.Forbidden };
            });
        builder.Services.AddSingleton(wrapper.Object);
        app = builder.Build();
        app.UseStaticFiles();
        Static(root + "/src/Presentation/XFramework.Portal.Features.POS/wwwroot", "/_content/XFramework.Portal.Features.POS");
        var configuration = new DirectoryInfo(TestContext.CurrentContext.TestDirectory).Parent!.Name;
        Static(root + $"/src/Presentation/XFramework.Portal.Features.POS/obj/{configuration}/net10.0/scopedcss/projectbundle", "/_content/XFramework.Portal.Features.POS");
        Static(root + "/src/Presentation/XFramework.Portal/wwwroot/css", "/cashier-css");
        app.UseAntiforgery();
        app.MapRazorComponents<PosPairingFixtureRoot>().AddInteractiveServerRenderMode();
        await app.StartAsync();

        void Static(string directory, string path) => app.UseStaticFiles(new StaticFileOptions
        { FileProvider = new PhysicalFileProvider(directory), RequestPath = path });
    }

    [OneTimeTearDown]
    public async Task Stop() { if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); } }

    [SetUp]
    public async Task Load()
    {
        // A disconnected Blazor circuit can retain its component briefly; old fixture pairings fail closed.
        state.ActivePairings.Clear();
        state.Paired = false; state.Pending = false;
        state.ExpirySeconds = 120; state.NextPairingCode = 6; state.Claims.Clear();
        state.Polls = 0; state.PausedPolls = 0; state.Revokes = 0; state.Acknowledged = 0;
        state.QueuedScan = null; state.LatestPairing = null; state.Cashier = null; state.FinancialCalls = 0;
        state.RevokedPairings.Clear(); state.CatalogRequests.Clear();
        var runtime = await Page.APIRequest.GetAsync(app.Urls.Single() + "/_framework/blazor.web.js");
        runtime.Status.Should().Be(200, "the framework script must be served from the built Portal's asset manifest");
        Page.PageError += (_, message) => TestContext.Progress.WriteLine(message);
        var response = await Page.GotoAsync(app.Urls.Single() + "/scanner-pairing-fixture");
        response!.Status.Should().Be(200, await Page.ContentAsync());
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Scan with phone", Exact = true })).ToBeEnabledAsync();
    }

    [TestCase(390,844)]
    [TestCase(1366,768)]
    public async Task PairingDialog_CloseKeepsPolling_PaymentPausesDelivery_ExplicitDisconnectRevokes(int width, int height)
    {
        await Page.SetViewportSizeAsync(width,height);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Scan with phone", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Phone scanner", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("pairing-short-code")).ToHaveTextAsync("000007");
        (await Page.GetByRole(AriaRole.Dialog).EvaluateAsync<bool>("e=>{const r=e.getBoundingClientRect();return r.left>=0 && r.right<=innerWidth}")).Should().BeTrue();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory,"artifacts","pos-scanner");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path=Path.Combine(directory,$"pairing-dialog-{width}.png"),FullPage=true });
        var before = Volatile.Read(ref state.Polls);
        await Page.GetByRole(AriaRole.Button,new() {Name="Done",Exact=true}).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        await Wait(() => Volatile.Read(ref state.Polls)>before);
        Volatile.Read(ref state.Revokes).Should().Be(0,"closing pairing content must not dispose the outer polling component");

        await Page.GetByRole(AriaRole.Button,new() {Name="Pause cashier",Exact=true}).ClickAsync();
        await Wait(() => Volatile.Read(ref state.PausedPolls)>0);
        state.Pending = true;
        await Page.WaitForTimeoutAsync(1200);
        await Expect(Page.GetByTestId("fixture-received")).ToHaveTextAsync("0");
        Interlocked.Read(ref state.Acknowledged).Should().Be(0);
        await Page.GetByRole(AriaRole.Button,new() {Name="Resume cashier",Exact=true}).ClickAsync();
        await Expect(Page.GetByTestId("fixture-received")).ToHaveTextAsync("1");
        await Wait(() => Interlocked.Read(ref state.Acknowledged)==1);
        await Page.GetByRole(AriaRole.Button,new() {Name="Scan with phone",Exact=true}).ClickAsync();
        await Page.GetByRole(AriaRole.Button,new() {Name="Disconnect",Exact=true}).ClickAsync();
        await Wait(() => Volatile.Read(ref state.Revokes)==1);
    }

    private static async Task Wait(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!predicate()) await Task.Delay(50,timeout.Token);
    }

    [TestCase(390,844)]
    [TestCase(1366,768)]
    public async Task ShortPairing_PhoneLandingPopover_KeyboardOutsideFocusAndSeparateQr(int width,int height)
    {
        await Page.SetViewportSizeAsync(width,height);
        var errors = new ConcurrentQueue<string>();
        Page.PageError += (_, message) => errors.Enqueue(message);
        await Page.GetByRole(AriaRole.Button,new() {Name="Scan with phone",Exact=true}).ClickAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeVisibleAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog,new() {Name="Scan with phone",Exact=true});
        var modalButtons = dialog.Locator("button:not([disabled])");
        await modalButtons.Last.FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(modalButtons.First).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(modalButtons.Last).ToBeFocusedAsync();
        var pairingUrl = new Uri(await DecodeQr("One-time phone pairing QR"));
        pairingUrl.Fragment.Should().Be("#" + new string('A',64));
        pairingUrl.Query.Should().Be("?tenant=" + state.TenantId);
        var trigger = Page.GetByRole(AriaRole.Button,new() {Name="Phone scanner",Exact=true});
        await trigger.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        var popover = Page.Locator(".scanner-phone-popover");
        await Expect(popover).ToBeVisibleAsync();
        await Expect(popover).ToHaveAttributeAsync("aria-label","Phone scanner");
        var closePopover = Page.GetByRole(AriaRole.Button,new() {Name="Close phone scanner QR",Exact=true});
        await Expect(closePopover).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(closePopover).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(closePopover).ToBeFocusedAsync();
        var landingUrl = new Uri(await DecodeQr("Phone scanner HTTPS landing QR"));
        landingUrl.AbsoluteUri.Should().Be("https://scanner.fixture.invalid/pos/mobile-scanner?tenant=" + state.TenantId);
        landingUrl.Fragment.Should().BeEmpty("the landing QR is not a pairing credential");
        (await popover.EvaluateAsync<bool>("e=>{const r=e.getBoundingClientRect();return r.left>=0 && r.right<=innerWidth}")).Should().BeTrue();
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory,"artifacts","pos-scanner");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() {Path=Path.Combine(directory,$"pairing-landing-popover-{width}.png"),FullPage=true});
        await Page.GetByAltText("Phone scanner HTTPS landing QR").ClickAsync();
        await Expect(popover).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button,new() {Name="Close phone scanner QR",Exact=true}).FocusAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(popover).ToBeHiddenAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Space");
        await Expect(popover).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Heading,new() {Name="Scan with phone",Exact=true}).ClickAsync();
        await Expect(popover).ToBeHiddenAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeVisibleAsync();
        for (var i = 0; i < 3; i++)
        {
            await trigger.ClickAsync();
            await Expect(popover).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Button,new() {Name="Close phone scanner QR",Exact=true}).ClickAsync();
            await Expect(popover).ToBeHiddenAsync();
            await Expect(trigger).ToBeFocusedAsync();
        }
        await trigger.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(popover).ToBeVisibleAsync();
        await trigger.FocusAsync();
        await Expect(trigger).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(popover).ToBeHiddenAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeHiddenAsync();
        state.Revokes.Should().Be(0,"Escape closes pairing content without revoking its lifetime");
        errors.Should().BeEmpty("the safe outside-click helper must survive repeated disposal");
    }

    private Task<string> DecodeQr(string alt) => Page.GetByAltText(alt).EvaluateAsync<string>("""
        async image => {
            await image.decode();
            const scanner = await import('/_content/XFramework.Portal.Features.POS/scanner/scanner.js');
            const canvas = document.createElement('canvas');
            canvas.width = image.naturalWidth; canvas.height = image.naturalHeight;
            const ctx = canvas.getContext('2d'); ctx.drawImage(image,0,0);
            const pixels = ctx.getImageData(0,0,canvas.width,canvas.height);
            return (await scanner.decode(pixels))[0].text;
        }
        """);

    [Test]
    public async Task ShortPairing_CountdownTicks_ExpiryHidesCredentials_RefreshCreatesNewPairing()
    {
        state.ExpirySeconds = 5;
        await Page.GetByRole(AriaRole.Button,new() {Name="Scan with phone",Exact=true}).ClickAsync();
        await Expect(Page.GetByTestId("pairing-short-code")).ToHaveTextAsync("000007");
        var initial = await Page.GetByTestId("pairing-countdown").TextContentAsync();
        initial.Should().MatchRegex("^Expires in 00:0[1-5]$");
        await Expect(Page.GetByTestId("pairing-countdown")).Not.ToHaveTextAsync(initial!,new() {Timeout=3000});
        var original = state.LatestPairing!.PairingId;
        await Expect(Page.GetByTestId("pairing-expired")).ToBeVisibleAsync(new() {Timeout=10000});
        await Expect(Page.GetByTestId("pairing-short-code")).ToBeHiddenAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeHiddenAsync();
        state.ExpirySeconds = 120;
        await Page.GetByRole(AriaRole.Button,new() {Name="New pairing code",Exact=true}).ClickAsync();
        await Expect(Page.GetByTestId("pairing-short-code")).ToHaveTextAsync("000008");
        state.LatestPairing!.PairingId.Should().NotBe(original);
        await Wait(() => state.RevokedPairings.Contains(original));
    }

    [Test]
    public async Task ShortPairing_MobileManualEntry_ExactSixDigitsLeadingZeroAndDenial()
    {
        await Page.GotoAsync(app.Urls.Single() + "/pos/mobile-scanner?tenant=" + state.TenantId);
        await Page.GetByText("Enter pairing code", new() { Exact = true }).ClickAsync();
        var code = Page.GetByLabel("Six-digit pairing code",new() {Exact=true});
        var pair = Page.GetByRole(AriaRole.Button,new() {Name="Pair with code",Exact=true});
        await Expect(code).ToHaveAttributeAsync("inputmode","numeric");
        await Expect(code).ToHaveAttributeAsync("maxlength","6");
        foreach (var invalid in new[] {"","12345","abc123"})
        {
            await code.FillAsync(invalid);
            await code.BlurAsync();
            await Expect(pair).ToBeDisabledAsync();
        }
        await code.FillAsync("000008"); await code.BlurAsync();
        await pair.ClickAsync();
        await Expect(Page.GetByText("Pairing denied.",new() {Exact=true})).ToBeVisibleAsync();
        await code.FillAsync("000007"); await code.BlurAsync();
        await pair.ClickAsync();
        await Expect(Page.GetByTestId("scanner-status")).ToHaveTextAsync("Paired");
        state.Claims.Select(c=>c.PairingCode).Should().Equal("000008","000007");
        state.Claims.Should().OnlyContain(c=>c.Challenge == "" && c.Metadata.RequestedTenantId == state.TenantId);
    }

    [Test]
    public async Task ShortPairing_MobileFragment_RetainedInMemoryNotManualInputOrQuery()
    {
        await Page.GotoAsync(app.Urls.Single() + "/pos/mobile-scanner?tenant=" + state.TenantId + "#" + new string('A',64));
        await Expect(Page.GetByRole(AriaRole.Button,new() {Name="Pair desktop",Exact=true})).ToBeEnabledAsync();
        await Expect(Page.GetByLabel("Six-digit pairing code",new() {Exact=true})).ToBeHiddenAsync();
        new Uri(Page.Url).Fragment.Should().BeEmpty();
        await Page.GetByRole(AriaRole.Button,new() {Name="Pair desktop",Exact=true}).ClickAsync();
        await Expect(Page.GetByTestId("scanner-status")).ToHaveTextAsync("Paired");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Leave pairing", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Paired until", new() { Exact = false })).ToHaveCountAsync(0);
        await Expect(Page.GetByLabel("Product code", new() { Exact = true })).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Start camera", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Pause camera", Exact = true })).ToHaveCountAsync(0);
        state.Claims.Should().ContainSingle(c=>c.Challenge == new string('A',64) && c.PairingCode == "" && c.Metadata.RequestedTenantId == state.TenantId);
    }

    [TearDown]
    public async Task Diagnostics()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            TestContext.Progress.WriteLine(await Page.Locator("body").InnerTextAsync());
    }

    [TestCase(390,844)]
    [TestCase(1366,768)]
    public async Task MobileScanner_OneCameraControl_ManualSendAndCompactDisconnect(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(app.Urls.Single() + "/pos/mobile-scanner?tenant=" + state.TenantId + "#" + new string('A',64));
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Pair desktop", Exact = true })).ToBeEnabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Pair desktop", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("scanner-status")).ToHaveTextAsync("Paired");
        await Page.EvaluateAsync("""
            () => {
                const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 480;
                const ctx = canvas.getContext('2d'); ctx.fillStyle = 'white'; ctx.fillRect(0,0,640,480);
                window.scannerTestStream = canvas.captureStream(10);
                navigator.mediaDevices.getUserMedia = async () => scannerTestStream;
            }
            """);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Start camera", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Pause camera", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Start camera", Exact = true })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Pause camera", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Start camera", Exact = true })).ToBeVisibleAsync();
        (await Page.EvaluateAsync<bool>("scannerTestStream.getTracks().every(t => t.readyState === 'ended')")).Should().BeTrue();
        await Page.GetByText("Enter product code", new() { Exact = true }).ClickAsync();
        await Page.GetByLabel("Product code", new() { Exact = true }).FillAsync("SKU-1");
        await Page.GetByLabel("Product code", new() { Exact = true }).BlurAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send code", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("scanner-status")).ToHaveTextAsync("Sent to desktop");
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "pos-scanner");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"mobile-minimal-{width}.png"), FullPage = true });
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Leave pairing", Exact = true }).ClickAsync();
        await Expect(Page.GetByTestId("scanner-status")).ToHaveTextAsync("Disconnected");
    }

    [TestCase(390,844)]
    [TestCase(1366,768)]
    public async Task CashierMobileScanner_RealCashierPaymentPause_NewCustomerKeepsPairingAndDiscardsOldScans(int width,int height)
    {
        await Page.SetViewportSizeAsync(width,height);
        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        await Page.GetByRole(AriaRole.Button,new() {Name="Scan with phone",Exact=true}).ClickAsync();
        await Expect(Page.GetByAltText("One-time phone pairing QR")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button,new() {Name="Done",Exact=true}).ClickAsync();
        state.Paired = true;
        await Expect(Page.GetByRole(AriaRole.Button,new() {Name="Phone connected",Exact=true})).ToBeVisibleAsync();
        var original = state.LatestPairing!.PairingId;

        await Page.GetByTestId("pos-checkout").ClickAsync();
        await Expect(Page.GetByTestId("pos-payment-stage")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button,new() {Name="Phone connected",Exact=true})).ToBeDisabledAsync();
        await Wait(() => Volatile.Read(ref state.PausedPolls)>0);
        state.QueuedScan = new(original,1,"SKU-1");
        await Page.WaitForTimeoutAsync(1200);
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        state.CatalogRequests.Should().BeEmpty();
        Interlocked.Read(ref state.Acknowledged).Should().Be(0);

        await Page.GetByRole(AriaRole.Button,new() {Name="Back to cart",Exact=true}).ClickAsync();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("20.00");
        await Wait(() => Interlocked.Read(ref state.Acknowledged)==1);
        Volatile.Read(ref state.Revokes).Should().Be(0,"closing payment must preserve the same sale pairing");
        state.CatalogRequests.Should().ContainSingle(r => r.Search == "SKU-1" && r.CategoryId == null);

        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory,"artifacts","pos-scanner");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory,$"cashier-scanner-{width}.png"),FullPage=true });
        await Page.GetByTestId("pos-checkout").ClickAsync();
        var paused = Volatile.Read(ref state.PausedPolls);
        await Wait(() => Volatile.Read(ref state.PausedPolls)>paused);
        state.QueuedScan = new(original,2,"SKU-1");
        await state.Cashier!.ClearForFixture();
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("0.00");
        await Wait(() => Interlocked.Read(ref state.Acknowledged) == 2);
        await Expect(Page.GetByRole(AriaRole.Button,new() {Name="Phone connected",Exact=true})).ToBeEnabledAsync();
        state.LatestPairing!.PairingId.Should().Be(original);
        Volatile.Read(ref state.Revokes).Should().Be(0);
        await Page.WaitForTimeoutAsync(1200);
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("0.00");
        state.CatalogRequests.Should().ContainSingle("old queued code must not enter the new sale");
        state.QueuedScan = new(original,3,"SKU-1");
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync("10.00");
        Volatile.Read(ref state.FinancialCalls).Should().Be(0,"Pay only opened the actual payment stage; no financial submission was used");
    }

    [TestCase("loading")]
    [TestCase("tenant-missing")]
    [TestCase("module-disabled")]
    public async Task CashierMobileScanner_UnavailableCashier_RemainsMountedButHiddenAndPaused(string unavailable)
    {
        await Page.GotoAsync(app.Urls.Single() + "/cashier-scanner-fixture");
        await Page.GetByRole(AriaRole.Button,new() {Name="Scan with phone",Exact=true}).ClickAsync();
        await Page.GetByRole(AriaRole.Button,new() {Name="Done",Exact=true}).ClickAsync();
        var before = Volatile.Read(ref state.Polls);
        await Page.GetByTestId("pos-checkout").ClickAsync();
        await Expect(Page.GetByTestId("pos-payment-stage")).ToBeVisibleAsync();
        await state.Cashier!.UnavailableForFixture(unavailable);
        await Expect(Page.GetByTestId("cashier-scanner-pairing")).ToHaveCountAsync(1);
        await Expect(Page.GetByTestId("cashier-scanner-pairing")).ToBeHiddenAsync();
        await Expect(Page.Locator(".pos-cashier-header-actions")).ToBeHiddenAsync();
        await Expect(Page.GetByTestId("pos-payment-stage")).ToBeHiddenAsync();
        (await Page.Locator(".pos-cashier-content").EvaluateAsync<string>("e=>getComputedStyle(e).display")).Should().Be("none");
        await Wait(() => Volatile.Read(ref state.Polls)>before && Volatile.Read(ref state.PausedPolls)>0);
        Volatile.Read(ref state.Revokes).Should().Be(0,"hidden loading state must not unmount the pairing owner");
        state.CatalogRequests.Should().BeEmpty();
    }

    private static string RepoRoot()
    {
        for(var d=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);d is not null;d=d.Parent)
            if(Directory.Exists(Path.Combine(d.FullName,"src/Presentation/XFramework.Portal.Features.POS/wwwroot")))return d.FullName;
        throw new InvalidOperationException("Repository assets required");
    }
}

public sealed class PairingFixtureState
{
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid RegisterId { get; } = Guid.NewGuid();
    public Guid CashierId { get; } = Guid.NewGuid();
    public Guid CurrencyId { get; } = Guid.NewGuid();
    public PosCatalogItemResponse Item { get; } = new() { ProductId = Guid.NewGuid(), SKU = "SKU-1", DisplayName = "Scanner fixture item", ProductName = "Scanner fixture item", IsAvailable = true, Price = 10m };
    public volatile PosScannerPairingResponse? LatestPairing;
    public volatile PairingFixtureQueuedScan? QueuedScan;
    public volatile PosScannerFixtureCashier? Cashier;
    public ConcurrentQueue<Guid> RevokedPairings { get; } = new();
    public ConcurrentDictionary<Guid,byte> ActivePairings { get; } = new();
    public ConcurrentQueue<SearchPosCatalogRequest> CatalogRequests { get; } = new();
    public ConcurrentQueue<ClaimPosScannerPairingRequest> Claims { get; } = new();
    public int ExpirySeconds = 120;
    public int NextPairingCode = 6;
    public int FinancialCalls;
    public volatile bool Paired;
    public volatile bool Pending;
    public int Polls;
    public int PausedPolls;
    public int Revokes;
    public long Acknowledged;
}

public sealed record PairingFixtureQueuedScan(Guid PairingId,long Sequence,string Code);

[Route("/scanner-pairing-fixture")]
[Route("/cashier-scanner-fixture")]
[Route("/pos/mobile-scanner")]
public sealed class PosPairingFixtureRoot : ComponentBase
{
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0,"<!doctype html><html data-base-color='zinc' data-primary-color='green'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><link rel='stylesheet' href='_content/XFramework.Portal.Features.POS/XFramework.Portal.Features.POS.bundle.scp.css'><link rel='stylesheet' href='cashier-css/app.css'><style>body{margin:16px;font-family:Arial}button{gap:8px}.app-main{height:100dvh}.mobile-scanner{max-width:528px;margin:auto}</style></head><body>");
        b.OpenComponent(1,new Uri(Navigation.Uri).AbsolutePath switch
        {
            "/cashier-scanner-fixture" => typeof(PosScannerFixtureCashierSurface),
            "/pos/mobile-scanner" => typeof(MobileScanner),
            _ => typeof(PosPairingFixtureSurface)
        });
        b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender:false));
        b.CloseComponent();
        b.AddMarkupContent(2,"<script src='_framework/blazor.web.js'></script></body></html>");
    }
}

public sealed class PosScannerFixtureAuthentication : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
        new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name,"Scanner fixture cashier")],"fixture"))));
}

public sealed class PosScannerFixtureCashierSurface : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0,"main");b.AddAttribute(1,"class","app-main");
        b.OpenComponent<PosScannerFixtureCashier>(2);b.CloseComponent();
        b.OpenComponent<XfContainerPortalHost>(3);b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(4);b.CloseComponent();
        b.CloseElement();
    }
}

// Render the actual Cashier tree/methods, replacing only initial remote loading with fixture data.
public sealed class PosScannerFixtureCashier : Cashier
{
    [Inject] public PairingFixtureState State { get; set; } = null!;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    protected override void OnInitialized()
    {
        State.Cashier = this;
        Set("_loading",false);Set("_hasTenant",true);Set("_moduleEnabled",true);
        Set("_registers",new List<PosRegisterResponse> {new() {Id=State.RegisterId,Name="Fixture register",CurrencyId=State.CurrencyId}});
        Set("_selectedRegisterId",State.RegisterId.ToString());
        Set("_currencies",new List<CurrencyType> {new() {Id=State.CurrencyId,CurrencyIsoCode3="USD"}});
        Set("_catalogItems",new List<PosCatalogItemResponse> {State.Item});
        var type = typeof(Cashier).GetNestedType("CartLine",BindingFlags.NonPublic)!;
        var line = Activator.CreateInstance(type)!;
        type.GetProperty("ProductId")!.SetValue(line,State.Item.ProductId);
        type.GetProperty("DisplayName")!.SetValue(line,State.Item.DisplayName);
        type.GetProperty("UnitPrice")!.SetValue(line,10m);
        type.GetProperty("Quantity")!.SetValue(line,1m);
        ((IList)typeof(Cashier).GetField("_cart",Private)!.GetValue(this)!).Add(line);
    }
    protected override Task OnInitializedAsync() => Task.CompletedTask;
    public Task ClearForFixture() => InvokeAsync(() =>
    { typeof(Cashier).GetMethod("ClearCurrentCart",Private)!.Invoke(this,null); StateHasChanged(); });
    public Task UnavailableForFixture(string condition) => InvokeAsync(() =>
    { Set(condition switch {"loading"=>"_loading","tenant-missing"=>"_hasTenant",_=>"_moduleEnabled"},condition=="loading");StateHasChanged(); });
    private void Set(string field,object value) => typeof(Cashier).GetField(field,Private)!.SetValue(this,value);
}

public sealed class PosPairingFixtureSurface : ComponentBase
{
    [Inject] public PairingFixtureState State { get; set; } = null!;
    private bool disabled;
    private int received;
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<CashierScannerPairing>(0);
        b.AddAttribute(1,"RegisterId",State.RegisterId);
        b.AddAttribute(2,"Disabled",disabled);
        b.AddAttribute(3,"ScanReceived",EventCallback.Factory.Create<string>(this,_=>{received++;}));
        b.CloseComponent();
        b.OpenComponent<BbButton>(4);
        b.AddAttribute(5,"OnClick",EventCallback.Factory.Create<MouseEventArgs>(this,_=>{disabled=!disabled;}));
        b.AddAttribute(6,"ChildContent",(RenderFragment)(c=>c.AddContent(0,disabled?"Resume cashier":"Pause cashier")));
        b.CloseComponent();
        b.OpenElement(7,"output");b.AddAttribute(8,"data-testid","fixture-received");b.AddContent(9,received);b.CloseElement();
        b.OpenComponent<XfContainerPortalHost>(10);b.CloseComponent();
        b.OpenComponent<BbOverlayPortalHost>(11);b.CloseComponent();
    }
}
