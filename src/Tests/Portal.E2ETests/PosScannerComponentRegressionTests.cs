using System.Net;
using System.Reflection;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Features.POS.Scanner;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Unit")]
[Category("Module:POS")]
[Category("Area:Scanner")]
public sealed class PosScannerComponentRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public async Task Mobile_ConcurrentDecodedPairing_EntersOnceBeforeJavaScriptAwait()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var script = new Mock<IJSObjectReference>();
        script.Setup(s => s.InvokeAsync<string>("pairingChallenge", It.IsAny<object?[]?>())).Returns(new ValueTask<string>(pending.Task));
        script.Setup(s => s.InvokeAsync<string>("pairingTenant", It.IsAny<object?[]?>())).ReturnsAsync("");
        var wrapper = new Mock<IPOSServiceWrapper>();
        wrapper.Setup(w => w.ClaimPosScannerPairing(It.IsAny<ClaimPosScannerPairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<PosScannerPhoneResponse>
            { HttpStatusCode = HttpStatusCode.Forbidden, Message = "Denied" });
        var page = new EmptyMobile();
        Inject(page, "POS", wrapper.Object);
        Set(page, "_script", script.Object);
        await using var renderer = new EmptyRenderer();
        renderer.Attach(page);
        var first = renderer.Dispatcher.InvokeAsync(() => page.Decoded("pairing"));
        (await renderer.Dispatcher.InvokeAsync(() => page.Decoded("pairing"))).Should().BeFalse();
        pending.SetResult(new string('A', 64));
        (await first).Should().BeFalse();
        wrapper.Verify(w => w.ClaimPosScannerPairing(It.IsAny<ClaimPosScannerPairingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Mobile_ConcurrentSend_SendsSequenceOnce_AndNextPhysicalScanUsesNextSequence()
    {
        var pending = new TaskCompletionSource<CmdResponse<PosScannerSendResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequences = new List<long>();
        var wrapper = new Mock<IPOSServiceWrapper>();
        wrapper.Setup(w => w.SendPosScannerCode(It.IsAny<SendPosScannerCodeRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SendPosScannerCodeRequest request, CancellationToken _) =>
            {
                sequences.Add(request.Sequence);
                return request.Sequence == 1 ? pending.Task : Task.FromResult(Sent(request.Sequence));
            });
        var page = new EmptyMobile();
        Inject(page, "POS", wrapper.Object);
        Set(page, "_phone", new PosScannerPhoneResponse(Guid.NewGuid(), new string('A',64), "Register", DateTimeOffset.UtcNow.AddMinutes(30)));
        await using var renderer = new EmptyRenderer();
        renderer.Attach(page);
        var first = renderer.Dispatcher.InvokeAsync(() => page.Decoded("SKU-1"));
        (await renderer.Dispatcher.InvokeAsync(() => page.Decoded("SKU-1"))).Should().BeFalse();
        pending.SetResult(Sent(1));
        (await first).Should().BeTrue();
        (await renderer.Dispatcher.InvokeAsync(() => page.Decoded("SKU-1"))).Should().BeTrue();
        sequences.Should().Equal(1, 2);
    }

    [TestCase(false, true)]
    [TestCase(true, true)]
    [TestCase(true, false)]
    public async Task Cashier_CallbackFailureDisconnectsWithoutAck_ButOldContinuationCannotClobberReplacement(bool fail, bool replace)
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = Pairing();
        var replacement = Pairing();
        var wrapper = new Mock<IPOSServiceWrapper>();
        wrapper.Setup(w => w.PollPosScannerCodes(It.IsAny<PollPosScannerCodesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<PosScannerPollResponse>
            { HttpStatusCode = HttpStatusCode.OK, Response = new(true, DateTimeOffset.UtcNow.AddMinutes(30), [new(1,"SKU-1")]) });
        var page = new EmptyPairing();
        Inject(page, "POS", wrapper.Object);
        Inject(page, "Tenant", Mock.Of<IPortalTenantContext>());
        page.ScanReceived = EventCallback.Factory.Create<string>(this, async _ =>
        { entered.SetResult(); await pending.Task; });
        Set(page, "_pairing", original);
        await using var renderer = new EmptyRenderer();
        renderer.Attach(page);
        var polling = renderer.Dispatcher.InvokeAsync(() =>
            (Task)typeof(CashierScannerPairing).GetMethod("Poll", Private)!.Invoke(page, null)!);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (replace) await renderer.Dispatcher.InvokeAsync(() => Set(page, "_pairing", replacement));
        if (fail) pending.SetException(new InvalidOperationException("Lookup failed"));
        else pending.SetResult();
        await Task.Delay(100);
        if (replace) Get(page, "_pairing").Should().BeSameAs(replacement);
        else Get(page, "_pairing").Should().BeNull();
        Get(page, "_acknowledged").Should().Be(0L);
        wrapper.Verify(w => w.RevokePosScannerPairing(It.IsAny<RevokePosScannerPairingRequest>(), It.IsAny<CancellationToken>()),
            replace ? Times.Never() : Times.Once());
        await ((CancellationTokenSource)Get(page, "_lifetime")!).CancelAsync();
        await polling;
    }

    private static CmdResponse<PosScannerSendResponse> Sent(long sequence) => new()
    { HttpStatusCode = HttpStatusCode.OK, Response = new(sequence, false) };

    [TestCase(false)]
    [TestCase(true)]
    public async Task Cashier_PopoverClosedDuringFocusRegistration_DisposesLateTrapWithoutClobberingReplacement(bool reopen)
    {
        var pending = new TaskCompletionSource<IAsyncDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = new TrackedFocusTrap();
        var replacement = new TrackedFocusTrap();
        var focus = new Mock<IFocusManager>();
        focus.SetupSequence(f => f.TrapFocus(It.IsAny<ElementReference>()))
            .Returns(pending.Task).ReturnsAsync(replacement);
        var page = new EmptyPairing();
        Inject(page,"Focus",focus.Object);
        Inject(page,"Tenant",Mock.Of<IPortalTenantContext>());
        Set(page,"_phoneLinkOpen",true);
        await using var renderer = new EmptyRenderer();
        renderer.Attach(page);
        Task Ready() => renderer.Dispatcher.InvokeAsync(() =>
            (Task)typeof(CashierScannerPairing).GetMethod("PhoneContentReady",Private)!.Invoke(page,null)!);
        Task Close() => renderer.Dispatcher.InvokeAsync(() =>
            (Task)typeof(CashierScannerPairing).GetMethod("PhoneLinkOpenChanged",Private)!.Invoke(page,[false])!);
        var late = Ready();
        await Close();
        if (reopen) { Set(page,"_phoneLinkOpen",true); await Ready(); }
        pending.SetResult(original);
        await late;
        original.Disposals.Should().Be(1);
        if (reopen) Get(page,"_phoneFocusTrap").Should().BeSameAs(replacement);
        else Get(page,"_phoneFocusTrap").Should().BeNull();
        await Close();
        replacement.Disposals.Should().Be(reopen ? 1 : 0);
    }

    private sealed class TrackedFocusTrap : IAsyncDisposable
    {
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    [TestCase("stop")]
    [TestCase("dispose")]
    [TestCase("hidden")]
    public async Task Mobile_StartCompletionAfterStopOrLifetimeChange_CannotMarkCameraRunning(string cancellation)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var script = new Mock<IJSObjectReference>();
        script.Setup(s => s.InvokeAsync<bool>("start",It.IsAny<object?[]?>())).Returns(new ValueTask<bool>(pending.Task));
        var page = new EmptyMobile();
        Set(page,"_script",script.Object);
        await using var renderer = new EmptyRenderer();
        renderer.Attach(page);
        var start = renderer.Dispatcher.InvokeAsync(() => (Task)typeof(MobileScanner).GetMethod("StartCamera",Private)!.Invoke(page,null)!);
        if(cancellation=="dispose") Set(page,"_disposed",true);
        if(cancellation=="hidden") await renderer.Dispatcher.InvokeAsync(() => page.CameraFailed("Fixture page hidden"));
        else await renderer.Dispatcher.InvokeAsync(() => (Task)typeof(MobileScanner).GetMethod("StopCamera",Private)!.Invoke(page,null)!);
        pending.SetResult(true);
        await start;
        Get(page,"_cameraRunning").Should().Be(false);
        Get(page,"_busy").Should().Be(false);
    }
    private static PosScannerPairingResponse Pairing() => new(Guid.NewGuid(), new string('B',64), new string('A',64), DateTimeOffset.UtcNow.AddMinutes(2));
    private static Type Base(ComponentBase component) => component is MobileScanner ? typeof(MobileScanner) : typeof(CashierScannerPairing);
    private static void Inject(ComponentBase component, string name, object value) => Base(component).GetProperty(name, Private)!.SetValue(component,value);
    private static void Set(ComponentBase component, string name, object value) => Base(component).GetField(name, Private)!.SetValue(component,value);
    private static object? Get(ComponentBase component, string name) => Base(component).GetField(name, Private)!.GetValue(component);
    private sealed class EmptyMobile : MobileScanner
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b) { }
        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }
    private sealed class EmptyPairing : CashierScannerPairing { protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder b) { } }

    // Attach real components to a dispatcher without rendering unrelated UI children.
    private sealed class EmptyRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), LoggerFactory.Create(_ => { }))
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public void Attach(IComponent component) => AssignRootComponentId(component);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw exception;
    }
}
