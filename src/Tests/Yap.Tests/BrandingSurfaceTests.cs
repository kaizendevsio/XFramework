using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Components;
using Yap.Client.Pages;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class BrandingSurfaceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task SettingsUpdatesAndReconnect_UseTheCurrentPublicAppName(bool custom)
    {
        var brand = custom ? YapBranding.Default with { Name = "Alpha Chat" } : YapBranding.Default;
        var services = new ServiceCollection().AddLogging();
        var js = new Mock<IJSInProcessRuntime>();
        services.AddSingleton<IJSRuntime>(js.Object);
        services.AddSingleton<NavigationManager>(new Navigation());
        services.AddSingleton(new ChatApi(new HttpClient { BaseAddress = new("https://yap.test/") }));
        services.AddSingleton(p => new ChatState(null!, p.GetRequiredService<ChatApi>(), js.Object));
        services.AddSingleton<VoiceState>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var state = provider.GetRequiredService<ChatState>();
        typeof(ChatState).GetProperty(nameof(ChatState.Online))!.SetValue(state, false);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var menu = await RenderAsync<Settings>(renderer, brand);
            Assert.That(menu, Does.Contain($"About {brand.Name}").And.Contain($"Alerts while {brand.Name} is closed"));
            var app = await RenderAsync<Settings>(renderer, brand, new() { [nameof(Settings.Section)] = "app" });
            Assert.That(app, Does.Contain($"Install {brand.Name}").And.Contain($"Add {brand.Name} to your home screen")
                .And.Contain($"What's new in {brand.Name}").And.Contain(Yap.Client.AppRelease.Version));
            var installed = await RenderAsync<InstalledSettings>(renderer, brand, new() { [nameof(Settings.Section)] = "app" });
            Assert.That(installed, Does.Contain($"{brand.Name} is running as an app on this device"));
            Assert.That(await RenderAsync<UpdateNotice>(renderer, brand), Does.Contain($"A new version of {brand.Name} is ready."));
            Assert.That(await RenderAsync<ToastHost>(renderer, brand), Does.Contain($"Can’t reach {brand.Name}. Reconnecting…"));
            Assert.That(await RenderAsync<CallSettings>(renderer, brand), Does.Contain($"installing {brand.Name} cannot grant permanent access"));
            if (custom) Assert.That(menu + app + installed, Does.Not.Contain("About Yap").And.Not.Contain("Install Yap"));
        });
    }

    private static async Task<string> RenderAsync<T>(HtmlRenderer renderer, YapBranding brand,
        Dictionary<string, object>? parameters = null) where T : IComponent
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<T>(0);
            if (parameters is not null) builder.AddMultipleAttributes(1, parameters);
            builder.CloseComponent();
        };
        var page = await renderer.RenderComponentAsync<CascadingValue<YapBranding>>(ParameterView.FromDictionary(new Dictionary<string, object?>
        { [nameof(CascadingValue<YapBranding>.Value)] = brand, [nameof(CascadingValue<YapBranding>.ChildContent)] = content }));
        return WebUtility.HtmlDecode(page.ToHtmlString());
    }

    private sealed class InstalledSettings : Settings
    {
        protected override void OnInitialized()
        {
            base.OnInitialized();
            typeof(Settings).GetField("installed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, true);
        }
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://yap.test/", "https://yap.test/settings");
        protected override void SetNavigationLockState(bool value) { }
    }
}
