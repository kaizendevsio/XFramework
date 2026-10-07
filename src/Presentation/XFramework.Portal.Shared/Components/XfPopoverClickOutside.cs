using BlazorBlueprint.Primitives.Popover;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace XFramework.Portal.Shared.Components;

// Blueprint 3.16.0 can dispose its callback while listener registration is still in flight.
// Keep the callback tracked until that registration and its matching cleanup have completed.
public sealed class XfPopoverClickOutside : ComponentBase, IAsyncDisposable
{
    [CascadingParameter] private PopoverContext Context { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private Task _initialization = Task.CompletedTask;
    private IJSObjectReference? _module;
    private IJSObjectReference? _cleanup;
    private DotNetObjectReference<XfPopoverClickOutside>? _callback;
    private bool _disposed;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _initialization = RegisterAsync();
        return _initialization;
    }

    private async Task RegisterAsync()
    {
        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import",
                "./_content/BlazorBlueprint.Primitives/js/primitives/click-outside.js");
            if (_disposed)
                return;

            _callback = DotNetObjectReference.Create(this);
            _cleanup = await _module.InvokeAsync<IJSObjectReference>("onClickOutsideByIds",
                Context.ContentId, _callback, nameof(JsOnClickOutside), Context.TriggerId);
        }
        catch (JSDisconnectedException)
        {
            // The circuit disconnected while registration was in flight.
        }
    }

    [JSInvokable]
    public Task JsOnClickOutside() => _disposed ? Task.CompletedTask : InvokeAsync(() =>
    {
        if (!_disposed && Context.IsOpen)
            Context.Close();
    });

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            await _initialization;
            if (_cleanup is not null)
            {
                await _cleanup.InvokeVoidAsync("dispose");
                await _cleanup.DisposeAsync();
            }
            if (_module is not null)
                await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // There is no connected document left to remove listeners from.
        }
        finally
        {
            _callback?.Dispose();
        }
    }
}
