using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

public sealed class Branding(IJSRuntime js)
{
    public YapBranding Current { get; private set; } = YapBranding.Default;

    public async Task InitializeAsync() => Current = await js.InvokeAsync<YapBranding>("yap.branding.get");
    public async Task RefreshAsync() => Current = await js.InvokeAsync<YapBranding>("yap.branding.refresh");
}
