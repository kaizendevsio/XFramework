using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Yap.Client.Services;

namespace Yap.Client.Pages;

/// <summary>
/// Web push controls for the Settings page. Kept in a partial so the Razor markup stays one
/// self-contained block.
///
/// The toggle reports the browser's real permission state rather than a stored preference, because
/// a person can revoke notifications outside the app and "denied" cannot be recovered from a web
/// page - only from browser or OS settings. Re-prompting in that state does nothing, so the UI
/// says so instead of looping.
/// </summary>
public partial class Settings
{
    [Inject] private ChatApi Api { get; set; } = default!;

    private PushConfig? pushConfig;
    private string pushState = "unsupported";
    private bool pushSubscribed, pushBusy;
    private string? pushNotice;

    // "denied" is deliberately excluded: no web API can re-prompt, so an enabled toggle would do
    // nothing. Reopening Settings re-reads the permission once it is changed in browser settings.
    private bool PushAvailable => pushConfig?.Enabled == true && pushState is "default" or "granted";

    private string PushDetail => pushConfig?.Enabled != true
        ? State.User is null ? "Sign in to turn on notifications." : "Notifications are not configured on this server yet."
        : pushState switch
        {
            // iOS exposes the Push API only to a home-screen install on 16.4 or newer.
            "uninstalled" => "Add Yap to your Home Screen first, then turn notifications on from the installed app.",
            "unsupported" => "This browser cannot receive push notifications.",
            "denied" => "Notifications are blocked for Yap. Allow them in your browser or system settings; the app cannot ask again.",
            "granted" when pushSubscribed => "This device receives messages and calls while Yap is closed.",
            _ => "Get messages and calls while Yap is closed or in the background."
        };

    private async Task LoadPushAsync()
    {
        pushState = await JS.InvokeAsync<string>("yap.push.state");
        if (State.User is null)
        {
            // Signing in is what creates the credential a subscription is bound to.
            pushConfig = new PushConfig(false, null, 0);
            return;
        }

        try
        {
            pushConfig = await Api.GetAsync<PushConfig>("api/chat/push/config");
            pushSubscribed = pushConfig.Enabled && await JS.InvokeAsync<string?>("yap.push.endpoint") is not null;
        }
        catch (ChatApiException)
        {
            // Offline or a Notifications outage: show the feature as unavailable, never as broken.
            pushConfig = new PushConfig(false, null, 0);
        }
    }

    private async Task TogglePushAsync()
    {
        if (pushBusy || pushConfig is null) return;
        pushBusy = true;
        pushNotice = null;
        try
        {
            if (pushSubscribed) await DisablePushAsync();
            else await EnablePushAsync();
        }
        catch (ChatApiException)
        {
            pushNotice = "Could not reach the server. Try again when you are back online.";
        }
        finally
        {
            pushBusy = false;
            pushState = await JS.InvokeAsync<string>("yap.push.state");
        }
    }

    private async Task EnablePushAsync()
    {
        // No await before this call: the browser only honours a permission prompt while the click
        // that triggered it is still the active user gesture.
        var subscription = await JS.InvokeAsync<PushSubscriptionResult>("yap.push.enable", pushConfig!.PublicKey);
        if (subscription.Error is { } error)
        {
            pushNotice = error switch
            {
                "denied" => "You blocked notifications. Allow them in your browser or system settings to turn this on.",
                "dismissed" => "Notifications stay off until you allow them.",
                "unsupported" => "This browser cannot receive push notifications.",
                _ => "This device could not be registered for notifications."
            };
            return;
        }

        await Api.PostAsync("api/chat/push/subscribe", new
        {
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth,
            subscription.ExpiresAt,
            subscription.Label
        });
        pushSubscribed = true;
        pushNotice = "Notifications are on for this device.";
    }

    private async Task DisablePushAsync()
    {
        var endpoint = await JS.InvokeAsync<string?>("yap.push.disable");
        // Always tell the server, even when the browser had already dropped its side, so the row
        // does not linger and keep receiving pushes nothing will render.
        if (endpoint is not null)
            await Api.PostAsync("api/chat/push/unsubscribe", new { Endpoint = endpoint });
        pushSubscribed = false;
        pushNotice = "Notifications are off for this device.";
    }

    private sealed record PushConfig(bool Enabled, string? PublicKey, int Devices);

    private sealed record PushSubscriptionResult(
        string? Error,
        string Endpoint = "",
        string P256dh = "",
        string Auth = "",
        DateTime? ExpiresAt = null,
        string? Label = null);
}
