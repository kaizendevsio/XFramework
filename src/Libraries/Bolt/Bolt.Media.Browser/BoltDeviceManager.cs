namespace Bolt.Media.Browser;

/// <summary>
/// Browser media device enumeration and permission management.
/// Wraps navigator.mediaDevices.enumerateDevices() and Permissions API.
/// </summary>
public sealed class BoltDeviceManager : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BoltDeviceManager(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Bolt.Media.Browser/bolt-media.js");
        return _module;
    }

    public async Task<MediaDeviceInfo[]> EnumerateAudioInputsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<MediaDeviceInfo[]>("enumerateAudioInputs");
    }

    public async Task<MediaDeviceInfo[]> EnumerateVideoInputsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<MediaDeviceInfo[]>("enumerateVideoInputs");
    }

    public async Task<MediaDeviceInfo[]> EnumerateAudioOutputsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<MediaDeviceInfo[]>("enumerateAudioOutputs");
    }

    public async Task<PermissionStatus> CheckPermissionsAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<PermissionStatus>("checkPermissions");
    }

    public async Task<bool> RequestPermissionsAsync(bool audio = true, bool video = false)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("requestPermissions", audio, video);
    }

    public async Task<bool> IsWebCodecsSupportedAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isWebCodecsSupported");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null) await _module.DisposeAsync();
    }
}

public record MediaDeviceInfo(string DeviceId, string Label, string GroupId);

public record PermissionStatus(string Audio, string Video);
