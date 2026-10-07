using System.Net;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
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
using XFramework.Portal.Features.POS.Scanner;
using XFramework.Portal.Shared;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

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
        state = new();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Portal:ScannerPublicBaseUrl"] = "https://scanner.fixture.invalid" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(options => options.DetailedErrors = true);
        builder.Services.AddBlazorBlueprintComponents();
        builder.Services.AddScoped<XfPortalService>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPortalService>(sp => sp.GetRequiredService<XfPortalService>()));
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton(Mock.Of<IPortalTenantContext>(t => t.SelectedTenantId == state.TenantId));
        var wrapper = new Mock<IPOSServiceWrapper>();
        wrapper.Setup(w => w.CreatePosScannerPairing(It.IsAny<CreatePosScannerPairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CmdResponse<PosScannerPairingResponse>
            { HttpStatusCode = HttpStatusCode.OK, Response = new(Guid.NewGuid(), new string('B',64), new string('A',64), DateTimeOffset.UtcNow.AddMinutes(2)) });
        wrapper.Setup(w => w.PollPosScannerCodes(It.IsAny<PollPosScannerCodesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PollPosScannerCodesRequest request, CancellationToken _) =>
            {
                Interlocked.Increment(ref state.Polls);
                if (request.PauseDelivery) Interlocked.Increment(ref state.PausedPolls);
                if (!request.PauseDelivery) Interlocked.Exchange(ref state.Acknowledged, request.AcknowledgedSequence);
                return new QueryResponse<PosScannerPollResponse>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = new(state.Paired, DateTimeOffset.UtcNow.AddMinutes(30),
                        state.Pending && !request.PauseDelivery && request.AcknowledgedSequence == 0 ? [new(1,"SKU-1")] : [])
                };
            });
        wrapper.Setup(w => w.RevokePosScannerPairing(It.IsAny<RevokePosScannerPairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            { Interlocked.Increment(ref state.Revokes); return new CmdResponse<bool> { HttpStatusCode = HttpStatusCode.OK, Response = true }; });
        builder.Services.AddSingleton(wrapper.Object);
        app = builder.Build();
        Static(root + "/src/Presentation/XFramework.Portal.Features.POS/wwwroot", "/_content/XFramework.Portal.Features.POS");
        Static(root + "/src/Presentation/XFramework.Portal.Features.POS/obj/Debug/net10.0/scopedcss/projectbundle", "/_content/XFramework.Portal.Features.POS");
        var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages");
        Static(Path.Combine(packages,"blazorblueprint.components/3.16.0/staticwebassets"), "/_content/BlazorBlueprint.Components");
        Static(Path.Combine(packages,"blazorblueprint.primitives/3.16.0/staticwebassets"), "/_content/BlazorBlueprint.Primitives");
        Static(Path.Combine(packages,"microsoft.aspnetcore.app.internal.assets/10.0.0/_framework"), "/_framework");
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
        state.Paired = false; state.Pending = false;
        state.Polls = 0; state.PausedPolls = 0; state.Revokes = 0; state.Acknowledged = 0;
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
        var link = Page.GetByRole(AriaRole.Link, new() { Name = "Phone scanner" });
        (await link.GetAttributeAsync("href")).Should().Be("https://scanner.fixture.invalid/pos/mobile-scanner?tenant=" + state.TenantId);
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
    public volatile bool Paired;
    public volatile bool Pending;
    public int Polls;
    public int PausedPolls;
    public int Revokes;
    public long Acknowledged;
}

[Route("/scanner-pairing-fixture")]
public sealed class PosPairingFixtureRoot : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0,"<!doctype html><html data-base-color='zinc' data-primary-color='green'><head><base href='/'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/css/themes.css'><link rel='stylesheet' href='_content/BlazorBlueprint.Components/blazorblueprint.css'><style>body{margin:16px;font-family:Arial}button{gap:8px}</style></head><body>");
        b.OpenComponent<PosPairingFixtureSurface>(1);
        b.AddComponentRenderMode(new InteractiveServerRenderMode(prerender:false));
        b.CloseComponent();
        b.AddMarkupContent(2,"<script src='_framework/blazor.web.js'></script></body></html>");
    }
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
