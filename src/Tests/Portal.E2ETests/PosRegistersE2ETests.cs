using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:POS")]
[Category("Area:Registers")]
public sealed class PosRegistersE2ETests : PageTest
{
    private RegisterSettings _settings = null!;

    public override BrowserNewContextOptions ContextOptions()
    {
        _settings = RegisterSettings.Load();
        return new BrowserNewContextOptions
        {
            BaseURL = _settings.BaseUrl,
            StorageStatePath = _settings.StorageStatePath,
            IgnoreHTTPSErrors = _settings.IgnoreHttpsErrors,
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
            ReducedMotion = ReducedMotion.Reduce
        };
    }

    [SetUp]
    public async Task OpenRegistersAsync()
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
        else
        {
            await Page.GotoAsync("/pos/registers", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Registers", Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    [Test]
    public async Task Register_OpenEditRejectInvalidDraftAndDiscard_DoesNotMutateFixture()
    {
        await OpenRegisterDetailAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        var name = Page.GetByLabel("Name", new() { Exact = true });
        await Expect(name).ToHaveValueAsync(RegisterHeading(_settings.RegisterName));
        await name.FillAsync("");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Register name is required.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Discard", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Back to registers", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_settings.BaseUrl}/pos/registers");
    }

    [Test]
    [Explicit("Updates and restores a register in an explicitly authorized isolated tenant.")]
    [Category("StateMutation")]
    public async Task Register_IsolatedDescriptionUpdate_PersistsAfterReloadAndRestoresOriginal()
    {
        Assert.That(PosCashierE2ESettings.Optional("ALLOW_REGISTER_UPDATE"), Is.EqualTo("1"),
            "Set POS_E2E_ALLOW_REGISTER_UPDATE=1 only for the isolated register-update fixture.");
        Assert.That(PosCashierE2ESettings.Optional("ISOLATED_TENANT_ID"), Is.EqualTo(_settings.TenantId),
            "POS_E2E_ISOLATED_TENANT_ID must exactly match POS_E2E_TENANT_ID.");

        await OpenRegisterDetailAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        var description = Page.GetByLabel("Description", new() { Exact = true });
        var original = await description.InputValueAsync();
        var updated = $"POS register save/reload E2E {Guid.NewGuid():N}";
        var restorationNeeded = false;

        try
        {
            await description.FillAsync(updated);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
            await Expect(Page.GetByText("POS register updated.", new() { Exact = true })).ToBeVisibleAsync();
            restorationNeeded = true;

            await Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
            await Expect(Page.GetByText(updated, new() { Exact = true })).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
            await Expect(Page.GetByLabel("Description", new() { Exact = true })).ToHaveValueAsync(updated);
        }
        finally
        {
            if (restorationNeeded)
            {
                var edit = Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true });
                if (await edit.IsVisibleAsync())
                    await edit.ClickAsync();

                await Page.GetByLabel("Description", new() { Exact = true }).FillAsync(original);
                await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
                await Expect(edit).ToBeVisibleAsync();
            }
        }
    }

    private async Task OpenRegisterDetailAsync()
    {
        var edit = Page.GetByRole(AriaRole.Button, new()
        {
            Name = $"Edit register {_settings.RegisterName}",
            Exact = true
        });
        await Expect(edit).ToBeVisibleAsync();
        await edit.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/pos/registers/[0-9a-f-]{36}$", RegexOptions.IgnoreCase));
        await Expect(Page.GetByTestId("pos-register-detail")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = RegisterHeading(_settings.RegisterName), Exact = true }))
            .ToBeVisibleAsync();
    }

    private static string RegisterHeading(string label)
    {
        var match = Regex.Match(label, @"^(?<name>.+) \([^()]+\)$");
        return match.Success ? match.Groups["name"].Value : label;
    }

    private sealed record RegisterSettings
    {
        public required string BaseUrl { get; init; }
        public string? StorageStatePath { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public required string TenantId { get; init; }
        public required string RegisterName { get; init; }
        public bool IgnoreHttpsErrors { get; init; }

        public static RegisterSettings Load()
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

            return new RegisterSettings
            {
                BaseUrl = baseUrl.TrimEnd('/'),
                StorageStatePath = storageState,
                Username = PosCashierE2ESettings.Optional("USERNAME"),
                Password = Environment.GetEnvironmentVariable("POS_E2E_PASSWORD"),
                TenantId = tenantId,
                RegisterName = Required("REGISTER_NAME"),
                IgnoreHttpsErrors = PosCashierE2ESettings.Optional("IGNORE_HTTPS_ERRORS") == "1"
            };
        }

        private static string Required(string suffix) => PosCashierE2ESettings.Optional(suffix)
            ?? throw new AssertionException($"Set POS_E2E_{suffix} before running POS register browser tests. "
                + "See src/Tests/Portal.E2ETests/POS-E2E.md. Tests never create or guess fixture data.");
    }
}
