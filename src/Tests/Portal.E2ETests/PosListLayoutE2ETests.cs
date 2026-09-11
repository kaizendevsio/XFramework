using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:POS")]
[Category("Area:ListLayout")]
public sealed class PosListLayoutE2ETests : PageTest
{
    private PosListSettings _settings = null!;

    public override BrowserNewContextOptions ContextOptions()
    {
        _settings = PosListSettings.Load();
        var (width, height) = TestViewport();
        return new BrowserNewContextOptions
        {
            BaseURL = _settings.BaseUrl,
            StorageStatePath = _settings.StorageStatePath,
            IgnoreHTTPSErrors = _settings.IgnoreHttpsErrors,
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = width, Height = height },
            ReducedMotion = ReducedMotion.Reduce
        };
    }

    [SetUp]
    public async Task SignInAsync()
    {
        Page.SetDefaultTimeout(15_000);
        Page.SetDefaultNavigationTimeout(30_000);

        var origin = JsonSerializer.Serialize(new Uri(_settings.BaseUrl).GetLeftPart(UriPartial.Authority));
        var tenant = JsonSerializer.Serialize(_settings.TenantId);
        await Context.AddInitScriptAsync($$"""
            if (location.origin === {{origin}}) {
                localStorage.setItem('xframework.portal.activeTenantId', {{tenant}});
            }
            """);

        if (_settings.StorageStatePath is null)
        {
            await Page.GotoAsync("/login?ReturnUrl=%2Fpos%2Fregisters", new() { WaitUntil = WaitUntilState.Load });
            await Page.GetByLabel("Username", new() { Exact = true }).FillAsync(_settings.Username!);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(_settings.Password!);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync($"{_settings.BaseUrl}/pos/registers", new() { Timeout = 30_000 });
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(768, 1024)]
    [TestCase(390, 844)]
    public async Task Lists_ResponsiveToolbarAndFilters_StayContainedAndReturnResults(int width, int height)
    {
        Page.ViewportSize.Should().BeEquivalentTo(new { Width = width, Height = height });

        foreach (var page in ListPages)
        {
            await Page.GotoAsync(page.Path, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = page.Heading, Exact = true }))
                .ToBeVisibleAsync(new() { Timeout = 30_000 });

            var surface = Page.GetByTestId("pos-list-surface");
            var toolbar = Page.GetByTestId("pos-list-toolbar");
            var searchField = Page.GetByTestId("pos-list-search");
            var statusField = Page.GetByTestId("pos-list-status");
            var searchButton = toolbar.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true });
            var pager = Page.GetByTestId("pos-list-pager");
            var searchInput = Page.GetByLabel(page.SearchLabel, new() { Exact = true });
            var statusSelect = Page.GetByLabel("Status", new() { Exact = true });

            await Expect(surface).ToBeVisibleAsync();
            await Expect(searchInput).ToHaveAccessibleNameAsync(page.SearchLabel);
            await Expect(statusSelect).ToHaveAccessibleNameAsync("Status");
            await Expect(statusField).ToContainTextAsync("All statuses");
            await Expect(searchButton).ToBeVisibleAsync();
            await AssertToolbarLayoutAsync(surface, toolbar, searchField, statusField, searchButton);
            await AssertPagerLayoutAsync(surface, pager);

            var screenshotDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "artifacts",
                "pos-list-layout",
                $"{width}x{height}");
            Directory.CreateDirectory(screenshotDirectory);
            var screenshot = Path.Combine(screenshotDirectory, $"{page.Slug}.png");
            await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
            TestContext.AddTestAttachment(screenshot, $"{page.Heading} at {width}x{height}");

            await SetSearchAsync(searchInput, $"no-match-{Guid.NewGuid():N}");
            await searchButton.ClickAsync();
            await Expect(Page.GetByTestId("pos-list-result-count"))
                .ToHaveTextAsync(new Regex("^0 "));
            await Expect(surface.GetByText(page.EmptyTitle, new() { Exact = true })).ToBeVisibleAsync();

            await SetSearchAsync(searchInput, "");
            await statusSelect.ClickAsync();
            await Page.GetByRole(AriaRole.Option, new() { Name = page.ExpectedStatus, Exact = true }).ClickAsync();
            await Expect(statusField).ToContainTextAsync(page.ExpectedStatus);
            await searchButton.ClickAsync();
            await Expect(Page.GetByTestId("pos-list-result-count"))
                .Not.ToHaveTextAsync(new Regex("^0 "));

            var rows = surface.Locator("tbody tr");
            var rowCount = await rows.CountAsync();
            rowCount.Should().BeGreaterThan(0, $"{page.Heading} should have a {page.ExpectedStatus} fixture row");
            for (var index = 0; index < rowCount; index++)
                await Expect(rows.Nth(index)).ToContainTextAsync(page.ExpectedStatus);
        }
    }

    private async Task SetSearchAsync(ILocator searchInput, string value)
    {
        await searchInput.FillAsync(value);
        await Expect(searchInput).ToHaveValueAsync(value);
        await searchInput.DispatchEventAsync("change");
        await Page.WaitForTimeoutAsync(500);
    }

    private static async Task AssertToolbarLayoutAsync(
        ILocator surface,
        ILocator toolbar,
        ILocator search,
        ILocator status,
        ILocator action)
    {
        var surfaceBox = await surface.BoundingBoxAsync();
        var toolbarBox = await toolbar.BoundingBoxAsync();
        var searchBox = await search.BoundingBoxAsync();
        var statusBox = await status.BoundingBoxAsync();
        var actionBox = await action.BoundingBoxAsync();

        surfaceBox.Should().NotBeNull();
        toolbarBox.Should().NotBeNull();
        searchBox.Should().NotBeNull();
        statusBox.Should().NotBeNull();
        actionBox.Should().NotBeNull();

        var bounds = surfaceBox!;
        foreach (var control in new[] { toolbarBox!, searchBox!, statusBox!, actionBox! })
        {
            control.X.Should().BeGreaterThanOrEqualTo(bounds.X - 1);
            (control.X + control.Width).Should().BeLessThanOrEqualTo(bounds.X + bounds.Width + 1);
        }

        (searchBox!.X < statusBox!.X + statusBox.Width
         && searchBox.X + searchBox.Width > statusBox.X
         && searchBox.Y < statusBox.Y + statusBox.Height
         && searchBox.Y + searchBox.Height > statusBox.Y)
            .Should().BeFalse("search and status controls must never overlap");
        (searchBox.X < actionBox!.X + actionBox.Width
         && searchBox.X + searchBox.Width > actionBox.X
         && searchBox.Y < actionBox.Y + actionBox.Height
         && searchBox.Y + searchBox.Height > actionBox.Y)
            .Should().BeFalse("search and action controls must never overlap");
        (statusBox.X < actionBox.X + actionBox.Width
         && statusBox.X + statusBox.Width > actionBox.X
         && statusBox.Y < actionBox.Y + actionBox.Height
         && statusBox.Y + statusBox.Height > actionBox.Y)
            .Should().BeFalse("status and action controls must never overlap");

        if (bounds.Width > 480)
        {
            Math.Abs((searchBox.Y + searchBox.Height) - (actionBox!.Y + actionBox.Height)).Should().BeLessThan(2,
                "inline toolbar controls should share a bottom baseline");
            Math.Abs((statusBox.Y + statusBox.Height) - (actionBox.Y + actionBox.Height)).Should().BeLessThan(2,
                "inline toolbar controls should share a bottom baseline");

            if (bounds.Width > 672)
            {
                searchBox.Width.Should().BeLessThanOrEqualTo(385);
                statusBox.Width.Should().BeLessThanOrEqualTo(193);
            }
        }
        else if (bounds.Width <= 480)
        {
            searchBox!.Y.Should().BeLessThan(statusBox!.Y);
            statusBox.Y.Should().BeLessThan(actionBox!.Y);
        }
    }

    private static async Task AssertPagerLayoutAsync(ILocator surface, ILocator pager)
    {
        var surfaceBox = await surface.BoundingBoxAsync();
        var pagerBox = await pager.BoundingBoxAsync();
        var previousBox = await pager.GetByRole(AriaRole.Button, new() { Name = "Previous", Exact = true }).BoundingBoxAsync();
        var nextBox = await pager.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).BoundingBoxAsync();

        surfaceBox.Should().NotBeNull();
        pagerBox.Should().NotBeNull();
        previousBox.Should().NotBeNull();
        nextBox.Should().NotBeNull();

        var bounds = surfaceBox!;
        foreach (var element in new[] { pagerBox!, previousBox!, nextBox! })
        {
            element.X.Should().BeGreaterThanOrEqualTo(bounds.X - 1);
            (element.X + element.Width).Should().BeLessThanOrEqualTo(bounds.X + bounds.Width + 1,
                "pager content must remain inside the POS list surface");
        }

        (previousBox!.X + previousBox.Width).Should().BeLessThanOrEqualTo(nextBox!.X,
            "pager buttons must not overlap");
    }

    private static readonly PosListPage[] ListPages =
    [
        new("registers", "/pos/registers", "Registers", "Name, code, or description", "Enabled", "No registers found"),
        new("sales", "/pos/sales", "Sales", "Receipt or idempotency key", "Completed", "No sales found"),
        new("returns", "/pos/returns", "Returns", "Return or idempotency key", "Completed", "No returns found")
    ];

    private static (int Width, int Height) TestViewport() =>
        TestContext.CurrentContext.Test.Arguments is [int width, int height]
            ? (width, height)
            : (1366, 768);

    private sealed record PosListPage(
        string Slug,
        string Path,
        string Heading,
        string SearchLabel,
        string ExpectedStatus,
        string EmptyTitle);

    private sealed record PosListSettings
    {
        public required string BaseUrl { get; init; }
        public string? StorageStatePath { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public required string TenantId { get; init; }
        public bool IgnoreHttpsErrors { get; init; }

        public static PosListSettings Load()
        {
            var baseUrl = PosCashierE2ESettings.Optional("BASE_URL") ?? "http://127.0.0.1:5267";
            Assert.That(Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                        && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo),
                Is.True, "POS_E2E_BASE_URL must be an absolute HTTP(S) URL without embedded credentials.");

            var storageState = PosCashierE2ESettings.Optional("STORAGE_STATE");
            if (storageState is not null)
            {
                storageState = Path.GetFullPath(storageState);
                Assert.That(File.Exists(storageState), Is.True, "POS_E2E_STORAGE_STATE does not exist.");
            }
            else
            {
                Required("USERNAME");
                Required("PASSWORD");
            }

            var tenantId = Required("TENANT_ID");
            Assert.That(Guid.TryParse(tenantId, out var id) && id != Guid.Empty, Is.True,
                "POS_E2E_TENANT_ID must identify an existing authorized tenant.");

            return new PosListSettings
            {
                BaseUrl = baseUrl.TrimEnd('/'),
                StorageStatePath = storageState,
                Username = PosCashierE2ESettings.Optional("USERNAME"),
                Password = Environment.GetEnvironmentVariable("POS_E2E_PASSWORD"),
                TenantId = tenantId,
                IgnoreHttpsErrors = PosCashierE2ESettings.Optional("IGNORE_HTTPS_ERRORS") == "1"
            };
        }

        private static string Required(string suffix) => PosCashierE2ESettings.Optional(suffix)
            ?? throw new AssertionException($"Set POS_E2E_{suffix} before running POS list layout browser tests. "
                + "See src/Tests/Portal.E2ETests/POS-E2E.md. Tests never create or guess fixture data.");
    }
}
