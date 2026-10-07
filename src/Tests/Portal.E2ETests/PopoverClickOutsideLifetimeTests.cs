using System.Reflection;
using BlazorBlueprint.Primitives.Popover;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using XFramework.Portal.Shared.Components;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:EntityPicker")]
public sealed class PopoverClickOutsideLifetimeTests
{
    [Test]
    public async Task DisposeAsync_PendingRegistration_KeepsCallbackTrackedUntilListenerIsRemoved()
    {
        var runtime = new Mock<IJSRuntime>();
        var module = new Mock<IJSObjectReference>();
        var cleanup = new Mock<IJSObjectReference>();
        var pending = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        DotNetObjectReference<XfPopoverClickOutside>? callback = null;
        runtime.Setup(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
            .ReturnsAsync(module.Object);
        module.Setup(js => js.InvokeAsync<IJSObjectReference>("onClickOutsideByIds", It.IsAny<object?[]>()))
            .Returns((string _, object?[] args) =>
            {
                callback = (DotNetObjectReference<XfPopoverClickOutside>)args[1]!;
                return new ValueTask<IJSObjectReference>(pending.Task);
            });
        var component = Create(runtime.Object);
        var registration = Start(component);
        callback.Should().NotBeNull();

        var disposal = component.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        callback!.Value.Should().BeSameAs(component);
        await component.JsOnClickOutside();
        cleanup.Invocations.Should().BeEmpty("the registration has not returned its cleanup handle yet");

        pending.SetResult(cleanup.Object);
        await registration;
        await disposal;
        cleanup.Invocations.Should().ContainSingle(call => call.Method.Name == "InvokeAsync" &&
            Equals(call.Arguments[0], "dispose"));
        cleanup.Verify(js => js.DisposeAsync(), Times.Once);
        module.Verify(js => js.DisposeAsync(), Times.Once);
        Action readDisposedReference = () => _ = callback.Value;
        readDisposedReference.Should().Throw<ObjectDisposedException>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RegisterAsync_DisconnectedCircuit_CompletesNormalTeardown(bool afterImport)
    {
        var runtime = new Mock<IJSRuntime>();
        var module = new Mock<IJSObjectReference>();
        if (afterImport)
        {
            runtime.Setup(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
                .ReturnsAsync(module.Object);
            module.Setup(js => js.InvokeAsync<IJSObjectReference>("onClickOutsideByIds", It.IsAny<object?[]>()))
                .ThrowsAsync(new JSDisconnectedException("Fixture circuit disconnected."));
        }
        else
            runtime.Setup(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
                .ThrowsAsync(new JSDisconnectedException("Fixture circuit disconnected."));
        var component = Create(runtime.Object);

        await Start(component);
        await component.DisposeAsync();
    }

    [Test]
    public async Task RegisterAsync_RealJavaScriptFailure_PropagatesInsteadOfSuppressingIt()
    {
        var runtime = new Mock<IJSRuntime>();
        runtime.Setup(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("Fixture initialization failure."));
        var component = Create(runtime.Object);

        Func<Task> registration = () => Start(component);
        await registration.Should().ThrowAsync<JSException>();
        Func<Task> disposal = () => component.DisposeAsync().AsTask();
        await disposal.Should().ThrowAsync<JSException>();
    }

    private static XfPopoverClickOutside Create(IJSRuntime runtime)
    {
        var component = new XfPopoverClickOutside();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(XfPopoverClickOutside).GetProperty("JS", flags)!.SetValue(component, runtime);
        typeof(XfPopoverClickOutside).GetProperty("Context", flags)!.SetValue(component, new PopoverContext());
        return component;
    }

    private static Task Start(XfPopoverClickOutside component) => (Task)typeof(XfPopoverClickOutside)
        .GetMethod("OnAfterRenderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, [true])!;
}
