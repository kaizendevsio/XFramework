using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Components;
using Yap.Client.Layout;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Tests;

/// <summary>The Calls tab as Recents rows and a call inside a conversation as one compact line.</summary>
public sealed class CallHistorySurfaceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly Guid Alex = Guid.NewGuid(), Sam = Guid.NewGuid(), Weekend = Guid.NewGuid();

    [Test]
    public async Task Recents_GroupConsecutiveCalls_AndExpandInPlaceWithCallAndMessageActions()
    {
        await using var provider = Services("calls");
        var items = History();
        var expanded = CallLog.Group(items, Section)[0].Groups[0].Key;
        var page = await RenderAsync(provider, b => Calls(b, items, expanded)); var html = Plain(page);

        Assert.That(Count(html, "<li class=\"call-group"), Is.EqualTo(4), "Seven calls, four rows: Alex x3, Sam, Weekend plans, then yesterday's Alex x2.");
        Assert.That(html, Does.Contain(">Today</h2>").And.Contain(">Yesterday</h2>"));
        Assert.That(html, Does.Contain("<span class=\"call-count\">(3)</span>"));
        Assert.That(html, Does.Contain("call-group missed open"), "The latest call was missed, so the row is tinted, and it is the open one.");
        Assert.That(Count(html, "aria-expanded=\"true\""), Is.EqualTo(1));
        Assert.That(html, Does.Contain("aria-label=\"Alex, 3 calls, Missed voice call, 5 min ago. Hide call details\""));
        Assert.That(html, Does.Contain("aria-label=\"Video call Sam\""), "The trailing button matches the last call's type.");
        Assert.That(html, Does.Contain($"href=\"/chat/{Alex}?message={items[0].Message.Id}\""));
        Assert.That(html, Does.Contain("Outgoing voice call<span class=\"call-log-duration\">5:09</span>"));
        Assert.That(html, Does.Not.Contain("call-card").And.Not.Contain("Call back"), "No per-call cards remain.");
        await SaveArtifactAsync("calls-grouped", page);
    }

    [Test]
    public async Task CallEvents_AreOneLinePills_WithMissedTintAndCallBackLabel()
    {
        await using var provider = Services($"chat/{Alex}");
        var missed = new ChatMessage { Id = Guid.NewGuid(), ThreadId = Alex, Text = "Missed voice call", IsCallSummary = true, CreatedAt = Now.AddMinutes(-50), Sender = "Alex" };
        var done = new ChatMessage { Id = Guid.NewGuid(), ThreadId = Alex, Text = "Voice call · 5:09", IsCallSummary = true, Mine = true, CreatedAt = Now.AddMinutes(-40), Sender = "Sam" };
        var video = new ChatMessage { Id = Guid.NewGuid(), ThreadId = Alex, Text = "Video call · 1:02", IsCallSummary = true, CreatedAt = Now.AddMinutes(-20), Sender = "Alex" };
        var page = await RenderAsync(provider, b => Conversation(b, missed, done, video)); var html = Plain(page);

        Assert.That(Count(html, "<button type=\"button\" class=\"call-event"), Is.EqualTo(3));
        Assert.That(html, Does.Contain("class=\"call-event missed\""));
        Assert.That(html, Does.Contain(">Missed voice call</span>").And.Contain(">Voice call · 5:09</span>").And.Contain(">Video call · 1:02</span>"));
        Assert.That(html, Does.Contain("aria-label=\"Voice call, 5 minutes 9 seconds, "));
        Assert.That(Count(html, ". Call back\""), Is.EqualTo(2), "The whole pill calls back while a session is live.");
        Assert.That(html, Does.Contain(". Video call back\""));
        Assert.That(html, Does.Not.Contain("call-card").And.Not.Contain(">Call back<"));
        await SaveArtifactAsync("conversation-call-events", page);
    }

    [Test]
    public async Task CallsTab_WithNoHistory_ExplainsWhatWillAppear()
    {
        // No live session and no device cache: the page must still settle on its empty state.
        await using var provider = Services("calls", live: false);
        var page = await RenderAsync(provider, b => { b.OpenComponent<Yap.Client.Pages.Calls>(0); b.CloseComponent(); }); var html = Plain(page);
        Assert.That(html, Does.Contain("<h2>No calls yet</h2>").And.Contain("href=\"/new\""));
        Assert.That(html, Does.Not.Contain("call-list"));
        await SaveArtifactAsync("calls-empty", page);
    }

    private static List<CallHistoryItem> History()
    {
        CallHistoryItem Call(Guid thread, string text, int minutesAgo, bool mine = false, string? name = null) =>
            new(new() { Id = Guid.NewGuid(), ThreadId = thread, Text = text, IsCallSummary = true, Mine = mine, CreatedAt = Now.AddMinutes(-minutesAgo) },
                name ?? (thread == Alex ? "Alex" : thread == Sam ? "Sam" : "Weekend plans"), thread == Weekend);
        return
        [
            Call(Alex, "Missed voice call", 5), Call(Alex, "Voice call · 5:09", 34, mine: true), Call(Alex, "Missed voice call", 40),
            Call(Sam, "Video call · 1:02", 95),
            Call(Weekend, "Voice call · 12:40", 180, mine: true),
            Call(Alex, "Missed video call", 26 * 60, mine: true), Call(Alex, "Voice call · 2:00", 27 * 60)
        ];
    }

    private static string Section(DateTime at) => at > Now.AddHours(-20) ? "Today" : "Yesterday";

    /// <summary>What time.js would answer, deterministically: the tests run on .NET, where formatting is fine.</summary>
    private static string Time(string identifier, object?[]? args)
    {
        var at = DateTime.UnixEpoch.AddMilliseconds(Convert.ToDouble(args![0], CultureInfo.InvariantCulture));
        return identifier switch
        {
            "yap.time.callSection" => Section(at),
            "yap.time.recent" => (Now - at).TotalMinutes < 60 ? $"{Math.Round((Now - at).TotalMinutes)} min ago" : at.ToString("h:mm tt", CultureInfo.InvariantCulture),
            "yap.time.clock" => at.ToString("h:mm tt", CultureInfo.InvariantCulture),
            "yap.time.dayMonth" => at.ToString("MMM d", CultureInfo.InvariantCulture),
            "yap.time.dayKey" => at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Section(at)
        };
    }

    private static ServiceProvider Services(string path, bool live = true)
    {
        var services = new ServiceCollection().AddLogging();
        var js = new Mock<IJSInProcessRuntime>();
        js.Setup(x => x.Invoke<string>(It.IsAny<string>(), It.IsAny<object?[]?>())).Returns((string id, object?[]? args) => Time(id, args));
        services.AddSingleton<IJSRuntime>(js.Object);
        services.AddSingleton<NavigationManager>(new Navigation(path));
        services.AddSingleton(new ChatApi(new HttpClient { BaseAddress = new("https://yap.test/") }));
        services.AddSingleton(p => new ChatState(null!, p.GetRequiredService<ChatApi>(), js.Object));
        services.AddSingleton<VoiceState>(); services.AddSingleton<BrowserTime>();
        var provider = services.BuildServiceProvider();
        var state = provider.GetRequiredService<ChatState>();
        typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(state, new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sam"));
        if (live) provider.GetRequiredService<ChatApi>().Account = state.Scope; // a live session: calling back is possible
        return provider;
    }

    private static async Task<string> RenderAsync(ServiceProvider provider, RenderFragment body)
    {
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<MainLayout>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Body"] = body }))).ToHtmlString());
    }

    private static void Calls(RenderTreeBuilder b, List<CallHistoryItem> items, Guid expanded)
    {
        b.OpenComponent<ScrollSurface>(0);
        b.AddAttribute(1, nameof(ScrollSurface.Header), (RenderFragment)(h =>
        {
            h.OpenElement(0, "div"); h.AddAttribute(1, "class", "appbar plain liquid-glass");
            h.OpenComponent<GlassLayer>(2); h.CloseComponent();
            h.AddMarkupContent(3, "<h1>Calls</h1><span class=\"sp\"></span><a class=\"iconbtn\" href=\"/new\" aria-label=\"Start a conversation\"><svg class=\"ic\" width=\"22\" height=\"22\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"m14 4 6 6M4 20l5-1L21 7a2 2 0 0 0-5-5L4 14v6Z\"/></svg></a>");
            h.CloseElement();
        }));
        b.AddAttribute(2, nameof(ScrollSurface.ChildContent), (RenderFragment)(c =>
        {
            c.OpenElement(0, "div"); c.AddAttribute(1, "class", "body calls-panel");
            c.OpenComponent<CallList>(2); c.AddAttribute(3, nameof(CallList.Items), items); c.AddAttribute(4, nameof(CallList.Expanded), (Guid?)expanded); c.CloseComponent();
            c.CloseElement();
        }));
        b.CloseComponent();
    }

    private static void Conversation(RenderTreeBuilder b, params ChatMessage[] calls)
    {
        ChatMessage Text(string text, bool mine, int minutesAgo) => new() { Id = Guid.NewGuid(), ThreadId = Alex, Text = text, Mine = mine, Sender = mine ? "Sam" : "Alex", CreatedAt = Now.AddMinutes(-minutesAgo) };
        var thread = new ChatMessage[] { Text("Are you free for a quick call?", false, 60), Text("Give me five minutes", true, 55), calls[0], calls[1], Text("Thanks, that helped a lot", false, 30), calls[2] };
        b.OpenComponent<ScrollSurface>(0);
        b.AddAttribute(1, nameof(ScrollSurface.Header), (RenderFragment)(h =>
        {
            h.OpenElement(0, "div"); h.AddAttribute(1, "class", "appbar liquid-glass");
            h.OpenComponent<GlassLayer>(2); h.CloseComponent();
            h.AddMarkupContent(3, "<span class=\"iconbtn\"></span><span class=\"mid\"><h1 style=\"font-size:16px\">Alex</h1></span>");
            h.CloseElement();
        }));
        b.AddAttribute(2, nameof(ScrollSurface.ChildContent), (RenderFragment)(c =>
        {
            // The real window positions rows from script; flow-root rows are what it measures.
            c.OpenElement(0, "div"); c.AddAttribute(1, "class", "body messages");
            c.AddMarkupContent(2, "<div class=\"message-date\"><time>Today</time></div>");
            foreach (var message in thread)
            {
                c.OpenElement(3, "div"); c.AddAttribute(4, "style", "display:flow-root");
                c.OpenComponent<MessageBubble>(5); c.AddAttribute(6, nameof(MessageBubble.Message), message); c.AddAttribute(7, nameof(MessageBubble.ShowTime), true); c.CloseComponent();
                c.CloseElement();
            }
            c.CloseElement();
        }));
        b.CloseComponent();
    }

    private static int Count(string html, string value) => html.Split(value).Length - 1;
    /// <summary>Markup without scoped-CSS attributes and with entities decoded, so assertions read as the page does.</summary>
    private static string Plain(string html) => WebUtility.HtmlDecode(Regex.Replace(html, " b-[a-z0-9]{10}", ""));

    /// <summary>With YAP_UI_ARTIFACT_DIR set, writes each surface as a standalone page with the real
    /// stylesheets in index.html's order, including the built scoped-CSS bundle, for screenshots.</summary>
    private static async Task SaveArtifactAsync(string name, string html)
    {
        if (Environment.GetEnvironmentVariable("YAP_UI_ARTIFACT_DIR") is not { Length: > 0 } directory) return;
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CLAUDE.md"))) root = root.Parent;
        var client = Path.Combine(root!.FullName, "src/Presentation/XFramework.Yap.Client");
        var bundle = new DirectoryInfo(Path.Combine(client, "obj")).EnumerateFiles("XFramework.Yap.Client.styles.css", SearchOption.AllDirectories)
            .Where(f => f.FullName.Contains("scopedcss", StringComparison.Ordinal)).OrderByDescending(f => f.LastWriteTimeUtc).First();
        var css = string.Join("\n", new[] { "app", "brand", "motion", "glass" }.Select(f => File.ReadAllText(Path.Combine(client, "wwwroot", f + ".css")))
            .Append(File.ReadAllText(bundle.FullName))
            .Concat(new[] { "mobile", "details" }.Select(f => File.ReadAllText(Path.Combine(client, "wwwroot", f + ".css")))));
        Directory.CreateDirectory(directory);
        foreach (var theme in new[] { "light", "dark" })
            await File.WriteAllTextAsync(Path.Combine(directory, $"{name}-{theme}.html"), $"<!doctype html><html data-theme=\"{theme}\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>{css}</style><body>{html}</body></html>");
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation(string path) => Initialize("https://yap.test/", "https://yap.test/" + path);
        protected override void SetNavigationLockState(bool value) { }
    }
}
