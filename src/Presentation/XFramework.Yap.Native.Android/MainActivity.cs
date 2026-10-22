using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Java.Interop;
using System.Security.Cryptography;
using System.Text.Json;
using Yap.Native;

namespace Yap.Native.Android;

[Activity(Label = "Yap native benchmark", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private WebView web = null!;
    private LinearLayout root = null!;
    private FrameLayout videos = null!;
    private TextureView remote = null!, preview = null!;
    private NativeCoordinator coordinator = null!;
    private string origin = "", token = "";
    private TaskCompletionSource<bool>? permission;
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
        root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetOnApplyWindowInsetsListener(new SystemInsets());
        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var address = new EditText(this) { Text = "https://xeon-dev.tailed40e.ts.net:8443", TextSize = 12 };
        address.SetSingleLine(true);
        var open = new Button(this) { Text = "Open" };
        bar.AddView(address, new LinearLayout.LayoutParams(0, Dp(48), 1)); bar.AddView(open);
        root.AddView(bar);
        videos = new FrameLayout(this) { Visibility = ViewStates.Gone };
        remote = new TextureView(this); preview = new TextureView(this);
        videos.AddView(remote, new FrameLayout.LayoutParams(-1, -1));
        videos.AddView(preview, new FrameLayout.LayoutParams(Dp(104), Dp(146), GravityFlags.Top | GravityFlags.Right));
        root.AddView(videos, new LinearLayout.LayoutParams(-1, 0, 1));
        web = new WebView(this);
        web.Settings.JavaScriptEnabled = true; web.Settings.DomStorageEnabled = true;
        web.Settings.AllowFileAccess = false; web.Settings.AllowContentAccess = false;
        web.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
        web.SetWebViewClient(new Client(this));
        web.AddJavascriptInterface(new Bridge(this), "YapBenchmarkHost");
        root.AddView(web, new LinearLayout.LayoutParams(-1, 0, 1));
        SetContentView(root);
        coordinator = new NativeCoordinator(CreateVideoAsync, Path.Combine(FilesDir!.AbsolutePath, "benchmarks"));
        coordinator.Message += message => RunOnUiThread(() =>
        {
            if (!Trusted()) return;
            web.EvaluateJavascript($"window.dispatchEvent(new CustomEvent('yap-native-message',{{detail:{message}}}))", null);
            using var parsed = JsonDocument.Parse(message);
            if (parsed.RootElement.GetProperty("kind").GetString() == "finished") videos.Visibility = ViewStates.Gone;
        });
        open.Click += async (_, _) =>
        {
            await coordinator.HandleAsync("{\"action\":\"stop\"}");
            Navigate(address.Text ?? "");
        };
        Navigate(address.Text!);
    }
    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);
    private sealed class SystemInsets : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            return insets;
        }
    }
    private void Navigate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != "https" || !string.IsNullOrEmpty(parsed.UserInfo))
        { Toast.MakeText(this, "Use your HTTPS Yap origin", ToastLength.Long)!.Show(); return; }
        origin = parsed.GetLeftPart(UriPartial.Authority); token = "";
        web.LoadUrl(origin + "/diagnostics/native-media");
    }
    private bool Trusted() => Uri.TryCreate(web.Url, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Authority) == origin;
    private void InstallBridge()
    {
        if (!Trusted()) return;
        token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        // Token is captured only in the top document's closure; cross-origin subframes
        // cannot acquire it even though Android exposes the Java interface to frames.
        web.EvaluateJavascript($"window.yapNative={{send:(message)=>YapBenchmarkHost.post({JsonSerializer.Serialize(token, NativeJson.Default.String)},JSON.stringify(message))}};window.dispatchEvent(new Event('yap-native-ready'));", null);
    }
    private async Task<INativeVideo> CreateVideoAsync()
    {
        var completion = new TaskCompletionSource<INativeVideo>(TaskCreationOptions.RunContinuationsAsynchronously);
        RunOnUiThread(async () =>
        {
            try
            {
                if (CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted)
                {
                    permission = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    RequestPermissions(new[] { Manifest.Permission.Camera }, 1);
                    if (!await permission.Task) throw new InvalidOperationException("Camera permission was denied.");
                }
                videos.Visibility = ViewStates.Visible;
                var until = DateTime.UtcNow.AddSeconds(5);
                while ((!remote.IsAvailable || !preview.IsAvailable) && DateTime.UtcNow < until) await Task.Delay(30);
                if (!remote.IsAvailable || !preview.IsAvailable) throw new InvalidOperationException("Native surfaces did not become available.");
                completion.SetResult(new AndroidVideo(this, preview, remote));
            }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return await completion.Task;
    }
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        permission?.TrySetResult(grantResults.Length > 0 && grantResults[0] == Permission.Granted);
    }
    protected override void OnPause() { base.OnPause(); _ = coordinator.HandleAsync("{\"action\":\"stop\"}"); }
    protected override void OnDestroy() { _ = coordinator.DisposeAsync(); web.Destroy(); base.OnDestroy(); }
    private sealed class Client(MainActivity owner) : WebViewClient
    {
        public override void OnPageStarted(WebView? view, string? url, global::Android.Graphics.Bitmap? icon)
        { owner.token = ""; _ = owner.coordinator.HandleAsync("{\"action\":\"stop\"}"); }
        public override void OnPageFinished(WebView? view, string? url) => owner.InstallBridge();
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
            => request?.Url?.Scheme is not ("https" or "about");
    }
    private sealed class Bridge(MainActivity owner) : Java.Lang.Object
    {
        [JavascriptInterface, Export("post")]
        public void Post(string suppliedToken, string json) => owner.RunOnUiThread(async () =>
        {
            if (owner.token.Length == 0 || suppliedToken != owner.token || !owner.Trusted()) return;
            try
            {
                // Export only the last numeric report, never arbitrary paths or pairing secrets.
                if (json == "{\"action\":\"export\"}")
                {
                    if (owner.coordinator.LastReport is not { } path) return;
                    var text = await File.ReadAllTextAsync(path);
                    var clipboard = (ClipboardManager)owner.GetSystemService(ClipboardService)!;
                    clipboard.PrimaryClip = ClipData.NewPlainText("Yap numeric benchmark", text);
                    Toast.MakeText(owner, "Numeric report copied; also available through adb run-as", ToastLength.Long)!.Show(); return;
                }
                await owner.coordinator.HandleAsync(json);
            }
            catch (Exception ex)
            {
                owner.web.EvaluateJavascript($"window.dispatchEvent(new CustomEvent('yap-native-message',{{detail:{JsonSerializer.Serialize(new NativeNotice("error", ex.Message), NativeJson.Default.NativeNotice)}}}))", null);
            }
        });
    }
}
