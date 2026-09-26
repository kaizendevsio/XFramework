using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Layout;
using Yap.Client.Pages;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Tests;

/// <summary>Presence dots on the real Inbox and the attachment activity in the real conversation header.</summary>
public sealed class PresenceSurfaceTests
{
    private static readonly Guid Me = Guid.NewGuid(), Sam = Guid.NewGuid(), Alex = Guid.NewGuid(), Jamie = Guid.NewGuid(), Priya = Guid.NewGuid(), Mia = Guid.NewGuid();
    private static readonly Guid SamChat = Guid.NewGuid();

    [Test]
    public async Task Inbox_ShowsGreenAndOrangeDots_AndNoneForOfflineHiddenOrGroups()
    {
        var now = DateTime.UtcNow;
        await using var services = await ServicesAsync("");
        var state = services.Provider.GetRequiredService<ChatState>();
        state.Conversations.AddRange(
        [
            Direct(Guid.NewGuid(), "Mia Chen", Mia, now.AddSeconds(30), now, favorite: true, preview: "On my way"),
            Direct(Guid.NewGuid(), "Leo Park", Guid.NewGuid(), null, now.AddMinutes(-4), favorite: true, preview: "Thanks!"),
            Direct(SamChat, "Sam Reyes", Sam, now.AddSeconds(30), now, preview: "Photos from Saturday coming up", at: now.AddMinutes(-1)),
            Direct(Guid.NewGuid(), "Alex Kim", Alex, null, now.AddMinutes(-6), preview: "Sounds good, talk later", at: now.AddMinutes(-9)),
            new Conversation { Id = Guid.NewGuid(), Name = "Weekend plans", Group = true, Members = 4, PeerId = null, Preview = "Jamie: I'll bring snacks", LastMessageAt = now.AddMinutes(-20) },
            Direct(Guid.NewGuid(), "Jamie Lee", Jamie, null, now.AddHours(-3), preview: "See you tomorrow", at: now.AddHours(-2)),
            Direct(Guid.NewGuid(), "Priya Nair", Priya, null, null, preview: "Hides active status", at: now.AddHours(-4))
        ]);
        var html = await RenderAsync(services, b => { b.OpenComponent<Inbox>(0); b.CloseComponent(); });

        Assert.Multiple(() =>
        {
            Assert.That(Count(html, "class=\"presence active\""), Is.EqualTo(2), "Mia (favorite) and Sam are active now.");
            Assert.That(Count(html, "class=\"presence away\""), Is.EqualTo(2), "Leo (favorite) and Alex were seen minutes ago.");
            Assert.That(Count(html, "role=\"img\" aria-label=\"Active now\""), Is.EqualTo(2), "The status is part of the row's accessible name.");
            Assert.That(Count(html, "role=\"img\" aria-label=\"Away\""), Is.EqualTo(2));
            Assert.That(Row(html, "Weekend plans"), Does.Not.Contain("presence"), "Groups carry no dot.");
            Assert.That(Row(html, "Jamie Lee"), Does.Not.Contain("presence"), "Seen hours ago: offline, no dot.");
            Assert.That(Row(html, "Priya Nair"), Does.Not.Contain("presence"), "A hidden status sends no heartbeat, so no dot.");
        });
        await SaveArtifactAsync("inbox-presence", html);
    }

    [Test]
    public async Task ConversationHeader_ShowsAttachmentActivity_InPlaceOfStatus_AndObeysTheTypingSetting()
    {
        var now = DateTime.UtcNow;
        var detail = new Conversation { Id = SamChat, Name = "Sam Reyes", Members = 2, Features = 127,
            People = [new(Me, "You", "you"), new(Sam, "Sam Reyes", "sam", ActiveUntil: now.AddSeconds(40), LastActiveAt: now)] };
        await using var services = await ServicesAsync($"chat/{SamChat}", detail);
        var state = services.Provider.GetRequiredService<ChatState>();
        state.Conversations.Add(Direct(SamChat, "Sam Reyes", Sam, now.AddSeconds(40), now, preview: "Photos from Saturday coming up"));
        await using var renderer = new HtmlRenderer(services.Provider, services.Provider.GetRequiredService<ILoggerFactory>());
        var page = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MainLayout>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Body"] = (RenderFragment)(b => { b.OpenComponent<ConversationPage>(0); b.AddAttribute(1, nameof(ConversationPage.Id), SamChat); b.CloseComponent(); })
        })));
        async Task<string> Html() { await page.QuiescenceTask; return Plain(await renderer.Dispatcher.InvokeAsync(page.ToHtmlString)); }

        var before = await Html();
        Assert.That(before, Does.Contain(">Active now</span>"));
        Assert.That(before, Does.Contain("aria-label=\"Conversation details, Active now\""), "The header avatar's button speaks the status its dot shows.");
        Assert.That(before, Does.Contain("class=\"presence active\""));

        await renderer.Dispatcher.InvokeAsync(() => state.TypingChanged(SamChat, Sam, true, ChatActivity.Photo, 2));
        var sending = await Html();
        Assert.That(sending, Does.Contain(">Sending 2 photos…</span>"), "The activity takes the status line's place.");
        Assert.That(sending, Does.Contain("<span class=\"activity-live\" role=\"status\">Sending 2 photos…</span>"));
        Assert.That(sending, Does.Not.Contain(">Active now</span>"));
        await SaveArtifactAsync("conversation-sending-photos", sending);

        await renderer.Dispatcher.InvokeAsync(() => state.TypingChanged(SamChat, Sam, false, ChatActivity.Photo, 2));
        Assert.That(await Html(), Does.Contain(">Active now</span>"), "Cancel or send puts the status back.");

        state.Selected!.Features &= ~(int)ChatFeature.Typing;
        await renderer.Dispatcher.InvokeAsync(() => state.TypingChanged(SamChat, Sam, true, ChatActivity.Photo, 2));
        Assert.That(await Html(), Does.Not.Contain("Sending"), "Typing indicators off: no attachment activity either.");
    }

    private static Conversation Direct(Guid id, string name, Guid peer, DateTime? activeUntil, DateTime? lastSeen, bool favorite = false, string preview = "", DateTime? at = null) =>
        new() { Id = id, Name = name, Members = 2, PeerId = peer, PeerActiveUntil = activeUntil, PeerLastActiveAt = lastSeen, IsFavorite = favorite, Preview = preview, LastMessageAt = at ?? DateTime.UtcNow.AddMinutes(-30) };

    private sealed class Surface(ServiceProvider provider, SqliteConnection connection) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;
        public async ValueTask DisposeAsync() { await Provider.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<OfflineDatabase>
    {
        public OfflineDatabase CreateDbContext() => new(new DbContextOptionsBuilder<OfflineDatabase>().UseModel(OfflineDatabaseModel.Instance).UseSqlite(connection).Options);
    }

    private static async Task<Surface> ServicesAsync(string path, Conversation? detail = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var factory = new Factory(connection);
        await using (var db = factory.CreateDbContext()) await db.Database.EnsureCreatedAsync();
        var services = new ServiceCollection().AddLogging();
        var js = new Mock<IJSInProcessRuntime>();
        js.Setup(x => x.Invoke<string>(It.IsAny<string>(), It.IsAny<object?[]?>())).Returns("14:30");
        services.AddSingleton<IJSRuntime>(js.Object);
        services.AddSingleton<NavigationManager>(new Navigation(path));
        services.AddSingleton(new ChatApi(new HttpClient(new Api(detail)) { BaseAddress = new("https://yap.test/") }));
        services.AddSingleton(p => new ChatState(new OfflineStore(factory), p.GetRequiredService<ChatApi>(), js.Object));
        services.AddSingleton<VoiceState>(); services.AddSingleton<BrowserTime>();
        var provider = services.BuildServiceProvider();
        typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(provider.GetRequiredService<ChatState>(), new UserSession(Me, Guid.NewGuid(), "You"));
        return new(provider, connection);
    }

    private sealed class Api(Conversation? detail) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            object body = path.EndsWith("/messages", StringComparison.Ordinal) ? new ChatPage<ChatMessage>([], 0) : detail ?? new Conversation();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) });
        }
    }

    private static async Task<string> RenderAsync(Surface services, RenderFragment body)
    {
        await using var renderer = new HtmlRenderer(services.Provider, services.Provider.GetRequiredService<ILoggerFactory>());
        return Plain(await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<MainLayout>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Body"] = body }))).ToHtmlString()));
    }

    /// <summary>The markup of the one inbox item that names <paramref name="name"/>.</summary>
    private static string Row(string html, string name) =>
        html.Split("<div class=\"inbox-item\"").Single(item => item.Contains($">{name}", StringComparison.Ordinal) && !item.Contains("<html", StringComparison.Ordinal));

    private static int Count(string html, string value) => html.Split(value).Length - 1;
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
        protected override void NavigateToCore(string uri, bool forceLoad) { }
        protected override void SetNavigationLockState(bool value) { }
    }
}
