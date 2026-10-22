using AVFoundation;
using CoreGraphics;
using Foundation;
using UIKit;
using WebKit;
using System.Text.Json;

namespace Yap.Native.iOS;

public sealed class MainController : UIViewController, IWKScriptMessageHandler
{
    private WKWebView web = null!;
    private UITextField address = null!;
    private UIButton open = null!;
    private UIView videos = null!, preview = null!, remote = null!;
    private NativeCoordinator coordinator = null!;
    private string origin = "";
    private IosVideo? current;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad(); View!.BackgroundColor = UIColor.Black;
        address = new UITextField { Text = "https://xeon-dev.tailed40e.ts.net:8443", TextColor = UIColor.White, Font = UIFont.SystemFontOfSize(12), AutocapitalizationType = UITextAutocapitalizationType.None, AutocorrectionType = UITextAutocorrectionType.No };
        open = UIButton.FromType(UIButtonType.System); open.SetTitle("Open", UIControlState.Normal);
        videos = new UIView { Hidden = true }; remote = new UIView(); preview = new UIView();
        videos.AddSubview(remote); videos.AddSubview(preview);
        var config = new WKWebViewConfiguration();
        config.UserContentController.AddScriptMessageHandler(this, "yapNative");
        config.UserContentController.AddUserScript(new WKUserScript(new NSString("window.yapNative={send:message=>window.webkit.messageHandlers.yapNative.postMessage(JSON.stringify(message))};"), WKUserScriptInjectionTime.AtDocumentStart, true));
        web = new WKWebView(CGRect.Empty, config) { NavigationDelegate = new Navigation(this) };
        View.AddSubviews(address, open, videos, web);
        coordinator = new NativeCoordinator(CreateVideoAsync, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "benchmarks"));
        coordinator.Message += message => BeginInvokeOnMainThread(() =>
        {
            if (!Trusted(web.Url?.AbsoluteString)) return;
            web.EvaluateJavaScript($"window.dispatchEvent(new CustomEvent('yap-native-message',{{detail:{message}}}))", (_, _) => { });
            using var parsed = JsonDocument.Parse(message);
            if (parsed.RootElement.GetProperty("kind").GetString() == "finished")
            { videos.Hidden = true; current = null; View.SetNeedsLayout(); }
        });
        open.TouchUpInside += async (_, _) => { await coordinator.HandleAsync("{\"action\":\"stop\"}"); Navigate(); };
        Navigate();
    }
    private bool Trusted(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Authority) == origin;
    private void Navigate()
    {
        if (!Uri.TryCreate(address.Text, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) return;
        origin = uri.GetLeftPart(UriPartial.Authority);
        web.LoadRequest(new NSUrlRequest(new NSUrl(origin + "/diagnostics/native-media")));
        address.ResignFirstResponder();
    }
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews(); var bounds = View!.Bounds; var top = View.SafeAreaInsets.Top;
        address.Frame = new CGRect(12, top, bounds.Width - 82, 42); open.Frame = new CGRect(bounds.Width - 66, top, 60, 42);
        var height = bounds.Height - top - 42 - View.SafeAreaInsets.Bottom;
        var videoHeight = videos.Hidden ? 0 : height * .48;
        videos.Frame = new CGRect(0, top + 42, bounds.Width, videoHeight);
        remote.Frame = videos.Bounds; preview.Frame = new CGRect(bounds.Width - 112, 8, 104, 146);
        web.Frame = new CGRect(0, top + 42 + videoHeight, bounds.Width, height - videoHeight);
        current?.Layout();
    }
    private Task<INativeVideo> CreateVideoAsync()
    {
        var completion = new TaskCompletionSource<INativeVideo>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (!await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video))
                    throw new InvalidOperationException("Camera permission was denied.");
                videos.Hidden = false; View!.SetNeedsLayout(); View.LayoutIfNeeded();
                current = new IosVideo(preview, remote); completion.SetResult(current);
            }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task;
    }
    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        if (!message.FrameInfo.MainFrame || !Trusted(message.FrameInfo.Request.Url?.AbsoluteString) || !Trusted(web.Url?.AbsoluteString)) return;
        _ = CommandAsync(message.Body.ToString() ?? "");
    }
    private async Task CommandAsync(string json)
    {
        try
        {
            if (json == "{\"action\":\"export\"}")
            {
                if (coordinator.LastReport is not { } path) return;
                var share = new UIActivityViewController(new NSObject[] { NSUrl.FromFilename(path) }, null);
                if (share.PopoverPresentationController is { } popover) { popover.SourceView = View; popover.SourceRect = new CGRect(0, 0, 1, 1); }
                PresentViewController(share, true, null); return;
            }
            await coordinator.HandleAsync(json);
        }
        catch (Exception ex) { web.EvaluateJavaScript($"window.dispatchEvent(new CustomEvent('yap-native-message',{{detail:{JsonSerializer.Serialize(new NativeNotice("error", ex.Message), NativeJson.Default.NativeNotice)}}}))", (_, _) => { }); }
    }
    public void Stop() => _ = coordinator.HandleAsync("{\"action\":\"stop\"}");
    private sealed class Navigation(MainController owner) : WKNavigationDelegate
    {
        public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation) => owner.Stop();
    }
}
