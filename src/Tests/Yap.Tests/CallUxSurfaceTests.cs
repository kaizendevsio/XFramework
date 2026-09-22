using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Components;
using Yap.Client.Layout;
using Yap.Client.Pages;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class CallUxSurfaceTests
{
    [Test]
    public async Task Shell_KeepsFourTabsOutsidePage_AndRendersFavoritesAndCallCards()
    {
        var services = new ServiceCollection().AddLogging();
        var js = new Mock<IJSInProcessRuntime>();
        js.Setup(x => x.Invoke<string>(It.IsAny<string>(), It.IsAny<object?[]?>())).Returns("Today 14:30");
        services.AddSingleton<IJSRuntime>(js.Object);
        services.AddSingleton<NavigationManager>(new Navigation());
        services.AddSingleton(new ChatApi(new HttpClient { BaseAddress = new("https://yap.test/") }));
        services.AddSingleton(p => new ChatState(null!, p.GetRequiredService<ChatApi>(), js.Object));
        services.AddSingleton<VoiceState>(); services.AddSingleton<BrowserTime>();
        await using var provider = services.BuildServiceProvider();
        var state = provider.GetRequiredService<ChatState>();
        typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(state, new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sam"));
        state.Conversations.AddRange([new() { Id = Guid.NewGuid(), Name = "Alex", IsFavorite = true }, new() { Id = Guid.NewGuid(), Name = "Jamie", IsFavorite = true }, new() { Id = Guid.NewGuid(), Name = "Weekend plans", Preview = "See you tomorrow" }]);
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment inbox = b => { b.OpenComponent<Inbox>(0); b.CloseComponent(); };
            var page = await renderer.RenderComponentAsync<MainLayout>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Body"] = inbox }));
            var html = page.ToHtmlString();
            Assert.That(html, Does.Contain("favorite-grid"));
            Assert.That(html, Does.Match("</div>\\s*<div class=\"shell-tabs\""));
            Assert.That(html, Does.Contain("aria-label=\"Calls\""));
            Assert.That(html.Split("aria-label=\"Sections\"").Length, Is.EqualTo(2), "Only the layout owns navigation.");
            await SaveArtifactAsync("inbox", html);
            RenderFragment cards = b =>
            {
                b.OpenElement(0, "div"); b.AddAttribute(1, "class", "body");
                b.OpenElement(2, "h1"); b.AddContent(3, "Calls"); b.CloseElement();
                foreach (var text in new[] { "Video call · 2:05", "Missed video call", "Voice call · 0:42" })
                {
                    b.OpenComponent<CallCard>(4);
                    b.AddAttribute(5, "Message", new ChatMessage { Id = Guid.NewGuid(), ThreadId = Guid.NewGuid(), Text = text, IsCallSummary = true, CreatedAt = DateTime.UtcNow });
                    b.CloseComponent();
                }
                b.CloseElement();
            };
            var calls = await renderer.RenderComponentAsync<MainLayout>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Body"] = cards }));
            var callHtml = calls.ToHtmlString();
            Assert.That(callHtml, Does.Contain("Video call back"));
            Assert.That(callHtml, Does.Contain("Missed video call"));
            Assert.That(callHtml, Does.Contain("Call back"));
            await SaveArtifactAsync("calls", callHtml);
        });
    }

    private static async Task SaveArtifactAsync(string name, string html)
    {
        if (Environment.GetEnvironmentVariable("YAP_UI_ARTIFACT_DIR") is not { Length: > 0 } directory) return;
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CLAUDE.md"))) root = root.Parent;
        var css = string.Join("\n", new[] { "app", "brand", "motion", "glass", "mobile", "details" }
            .Select(file => File.ReadAllText(Path.Combine(root!.FullName, "src/Presentation/XFramework.Yap.Client/wwwroot", file + ".css"))));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), "<!doctype html><html data-theme=\"dark\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>" + css + "</style><body>" + html + "</body></html>");
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://yap.test/", "https://yap.test/");
        protected override void SetNavigationLockState(bool value) { }
    }
}
