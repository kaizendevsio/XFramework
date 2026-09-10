using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework.Interfaces;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:E2E")]
[Category("Module:POS")]
[Category("Area:Cashier")]
public sealed class PosCashierE2ETests : PageTest
{
    private readonly ConcurrentQueue<string> _pageErrors = new();
    private readonly ConcurrentQueue<string> _consoleErrors = new();
    private readonly ConcurrentQueue<string> _httpErrors = new();
    private PosCashierE2ESettings _settings = null!;
    private bool _tracing;
    private bool _cashierReady;

    private ILocator Search => Page.Locator("#pos-catalog-search");
    private ILocator Catalog => Page.GetByTestId("pos-catalog");
    private ILocator Tiles => Page.Locator(".pos-product-tile");
    private ILocator Lines => Page.Locator(".pos-cart-line");
    private ILocator Pay => Page.GetByTestId("pos-checkout");
    private ILocator Status => Page.Locator("#pos-checkout-status");
    private ILocator CashReceived => Page.GetByLabel("Cash received", new() { Exact = true });

    public override BrowserNewContextOptions ContextOptions()
    {
        _settings = PosCashierE2ESettings.Load();
        var (width, height, dark) = TestViewport();
        return new()
        {
            BaseURL = _settings.BaseUrl,
            StorageStatePath = _settings.StorageStatePath,
            IgnoreHTTPSErrors = _settings.IgnoreHttpsErrors,
            Locale = "en-US",
            ViewportSize = new() { Width = width, Height = height },
            ColorScheme = dark ? ColorScheme.Dark : ColorScheme.Light,
            ReducedMotion = ReducedMotion.Reduce
        };
    }

    [SetUp]
    public async Task OpenCashierAsync()
    {
        _pageErrors.Clear();
        _consoleErrors.Clear();
        _httpErrors.Clear();
        _cashierReady = false;
        _tracing = false;
        Page.SetDefaultTimeout(15_000);
        Page.SetDefaultNavigationTimeout(30_000);
        Page.PageError += (_, message) => _pageErrors.Enqueue(message);
        Page.Console += (_, message) =>
        {
            if (message.Type == "error")
                _consoleErrors.Enqueue($"{message.Text} ({message.Location})");
        };
        Page.Response += (_, response) =>
        {
            if (response.Status >= 400)
                _httpErrors.Enqueue($"{response.Status} {new Uri(response.Url).AbsolutePath}");
        };

        var origin = JsonSerializer.Serialize(new Uri(_settings.BaseUrl).GetLeftPart(UriPartial.Authority));
        var tenant = JsonSerializer.Serialize(_settings.TenantId);
        var dark = TestViewport().Dark ? "true" : "false";
        await Context.AddInitScriptAsync($$"""
            if (location.origin === {{origin}}) {
                localStorage.setItem('xframework.portal.activeTenantId', {{tenant}});
                const theme = JSON.parse(localStorage.getItem('bb-theme') || '{}');
                localStorage.setItem('bb-theme', JSON.stringify({ ...theme, isDarkMode: {{dark}} }));
            }
            """);

        // Do not record the credential form or authentication POST in failure traces.
        if (_settings.StorageStatePath is null)
        {
            try
            {
                await Page.GotoAsync("/login?ReturnUrl=%2Fpos%2Fcashier", new() { WaitUntil = WaitUntilState.Load });
                await Page.GetByLabel("Username", new() { Exact = true }).FillAsync(_settings.Username!);
                await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(_settings.Password!);
                var loginResponse = await Page.RunAndWaitForResponseAsync(
                    () => Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync(),
                    response => new Uri(response.Url).AbsolutePath == "/auth/login",
                    new() { Timeout = 15_000 });
                TestContext.Progress.WriteLine($"POS login endpoint returned HTTP {loginResponse.Status}.");
                await Expect(Page).ToHaveURLAsync($"{_settings.BaseUrl}/pos/cashier", new() { Timeout = 30_000 });
            }
            catch (Exception exception)
            {
                var formState = await Page.Locator("form.login-form").EvaluateAllAsync<string>("""
                    forms => forms.length === 0 ? 'Login form absent' : JSON.stringify({
                        usernamePresent: Boolean(forms[0].elements.username?.value),
                        passwordPresent: Boolean(forms[0].elements.password?.value),
                        formValid: forms[0].checkValidity()
                    })
                    """);
                throw new AssertionException($"POS login failed: {Redact(exception.Message)}\nForm state: {formState}");
            }
        }

        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        _tracing = true;
        if (_settings.StorageStatePath is not null)
            await Page.GotoAsync("/pos/cashier", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.GetByTestId("pos-cashier")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Search).ToHaveAccessibleNameAsync("Search products");
        await WaitForCatalogAsync("");
        // Verify initial loading before a register change can trigger another catalog request.
        await Expect(Tiles.First).ToBeVisibleAsync();
        await Expect(Lines).ToHaveCountAsync(0);

        var register = Page.GetByTestId("pos-register-picker").Locator("button.xf-entity-picker-trigger");
        await register.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = _settings.RegisterName, Exact = true }).ClickAsync();
        await Expect(register).ToContainTextAsync(_settings.RegisterName);
        await WaitForCatalogIdleAsync();
        _cashierReady = true;
    }

    [TearDown]
    public async Task CheckBrowserAndSaveFailureArtifactsAsync()
    {
        Exception? browserFailure = null;
        if (_cashierReady)
        {
            try
            {
                await AssertHealthyAsync();
            }
            catch (Exception exception)
            {
                browserFailure = exception;
            }
        }

        var testAlreadyFailed = TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed;
        var failed = testAlreadyFailed || browserFailure is not null;
        try
        {
            if (failed && Page is not null && !Page.IsClosed)
                await SaveFailureArtifactsAsync(browserFailure);
            else if (_tracing)
                await Context.Tracing.StopAsync();
        }
        catch (Exception exception)
        {
            TestContext.Error.WriteLine($"Could not finish POS browser artifacts: {exception.GetType().Name}: {Redact(exception.Message)}");
        }
        finally
        {
            _tracing = false;
        }

        if (browserFailure is not null && !testAlreadyFailed)
            ExceptionDispatchInfo.Capture(browserFailure).Throw();
    }

    [Test]
    public async Task Catalog_InitialLoad_ShowsProductsWithoutManualSearch()
    {
        await Expect(Search).ToHaveValueAsync("");
        await Expect(Tiles.First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("pos-catalog-empty")).ToBeHiddenAsync();
        await Expect(Pay).ToBeDisabledAsync();
        await Expect(Status).ToContainTextAsync(new Regex("add|empty|item"), new() { IgnoreCase = true });
        await Expect(Pay).ToHaveTextAsync(new Regex($@"^\s*Pay\s+{Regex.Escape(_settings.Currency)}\s+0\.00\s*$"));
    }

    [Test]
    public async Task Catalog_NameVariantAndNoMatchSearch_ShowsCurrentResults()
    {
        await SearchAsync(_settings.ProductName);
        await Expect(ProductTile(_settings.ProductName)).ToBeVisibleAsync();
        await Expect(ProductTile(_settings.ProductName)).ToContainTextAsync(_settings.ProductSku);
        await SearchAsync(_settings.VariantQuery);
        await Expect(ProductTile(_settings.VariantName)).ToBeVisibleAsync();
        await Expect(ProductTile(_settings.VariantName)).ToContainTextAsync(_settings.VariantSku);

        await SearchAsync(NoMatchQuery());
        await Expect(Tiles).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("pos-catalog-empty")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("pos-catalog-empty")).ToContainTextAsync(new Regex("no|not found"), new() { IgnoreCase = true });

        await SearchAsync("");
        await Expect(Tiles.First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("pos-catalog-empty")).ToBeHiddenAsync();
        await Expect(Lines).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Catalog_ClearedInputSubmitted_RestoresCatalogWithoutCircuitError()
    {
        await SearchAsync(NoMatchQuery());
        await Expect(Tiles).ToHaveCountAsync(0);
        await Search.ClearAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await WaitForCatalogAsync("");
        await Expect(Tiles.First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("pos-catalog-empty")).ToBeHiddenAsync();

        await SearchAsync(_settings.ProductName);
        await Search.ClearAsync();
        await Search.PressAsync("Enter");
        await WaitForCatalogAsync("");
        await Expect(Tiles.First).ToBeVisibleAsync();
        await Expect(Search).ToHaveValueAsync("");
        await Expect(Lines).ToHaveCountAsync(0);
        await AssertHealthyAsync();
    }

    [Test]
    public async Task Catalog_EnterWithUniqueSku_AddsProductOncePerScan()
    {
        await ScanAsync(_settings.ProductSku);
        await ExpectQuantityAsync(_settings.ProductName, 1);
        await ScanAsync(_settings.ProductSku);
        await ExpectQuantityAsync(_settings.ProductName, 2);
        await Expect(Lines).ToHaveCountAsync(1);
        await Expect(Search).ToHaveValueAsync("");
        await Expect(Search).ToBeFocusedAsync();
        await AssertCartTotalsAsync();
    }

    [Test]
    public async Task Catalog_VariantSku_OnlyAutoAddsWhenExactlyOneMatchExists()
    {
        await Search.FillAsync(_settings.VariantSku);
        await Search.PressAsync("Enter");
        await WaitForCatalogAsync(_settings.VariantSku);
        var exactMatches = Tiles.Filter(new()
        {
            Has = Page.Locator(".pos-product-meta").Filter(new()
            {
                HasTextRegex = new Regex($"^{Regex.Escape(_settings.VariantSku)}$", RegexOptions.IgnoreCase)
            })
        });
        await Expect(exactMatches).ToHaveCountAsync(_settings.VariantSkuMatchCount);
        await Expect(ProductTile(_settings.VariantName)).ToBeVisibleAsync();

        if (_settings.VariantSkuMatchCount == 1)
        {
            await Expect(Search).ToHaveValueAsync("");
            await ExpectQuantityAsync(_settings.VariantName, 1);
        }
        else
        {
            await Expect(Search).ToHaveValueAsync(_settings.VariantSku);
            await Expect(Lines).ToHaveCountAsync(0);
            await ProductTile(_settings.VariantName).ClickAsync();
            await ExpectQuantityAsync(_settings.VariantName, 1);
        }

        await Expect(Lines).ToHaveCountAsync(1);
        await AssertCartTotalsAsync();
    }

    [Test]
    public async Task Catalog_RapidConsecutiveQueries_KeepsLatestInputAndCircuitAlive()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var missing = NoMatchQuery();
            // Do not wait for search results between Enter events: exercise overlapping server requests.
            foreach (var query in new[] { _settings.ProductName, _settings.VariantQuery, missing })
            {
                await Search.FillAsync(query);
                await Search.PressAsync("Enter");
            }

            await WaitForCatalogAsync(missing);
            await Expect(Search).ToHaveValueAsync(missing);
            await Expect(Tiles).ToHaveCountAsync(0);
            await Expect(Page.GetByTestId("pos-catalog-empty")).ToBeVisibleAsync();
            await SearchAsync(_settings.ProductName);
            await Expect(ProductTile(_settings.ProductName)).ToBeVisibleAsync();
        }

        await ProductTile(_settings.ProductName).ClickAsync();
        await ExpectQuantityAsync(_settings.ProductName, 1);
        await AssertHealthyAsync();
    }

    [Test]
    public async Task Cart_AddChangeRemoveAndUndo_RestoresQuantityOrderAndTotals()
    {
        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        await AddProductAsync(_settings.VariantQuery, _settings.VariantName);
        var baseLine = CartLine(_settings.ProductName);
        await baseLine.GetByRole(AriaRole.Button, new() { Name = "Increase quantity", Exact = true }).ClickAsync();
        await ExpectQuantityAsync(_settings.ProductName, 2);
        await AssertCartTotalsAsync();
        await baseLine.GetByRole(AriaRole.Button, new() { Name = "Decrease quantity", Exact = true }).ClickAsync();
        await ExpectQuantityAsync(_settings.ProductName, 1);
        await Expect(baseLine.GetByRole(AriaRole.Button, new() { Name = "Decrease quantity", Exact = true })).ToBeDisabledAsync();
        await AssertCartTotalsAsync();
        await baseLine.GetByRole(AriaRole.Button, new() { Name = "Increase quantity", Exact = true }).ClickAsync();
        await ExpectQuantityAsync(_settings.ProductName, 2);

        var before = await Page.GetByTestId("pos-total").InnerTextAsync();
        var order = await Lines.GetByTestId("pos-cart-item-name").AllTextContentsAsync();
        await baseLine.GetByRole(AriaRole.Button, new() { Name = "Remove item", Exact = true }).ClickAsync();
        await Expect(baseLine).ToHaveCountAsync(0);
        await AssertCartTotalsAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Undo removal", Exact = true }).ClickAsync();
        await ExpectQuantityAsync(_settings.ProductName, 2);
        await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync(before);
        await Expect(Lines.GetByTestId("pos-cart-item-name")).ToHaveTextAsync(order);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Undo removal", Exact = true })).ToBeHiddenAsync();
        await AssertCartTotalsAsync();
    }

    [Test]
    public async Task Cash_InsufficientExactAndExcessTender_UpdatesReadinessAndChange()
    {
        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        await Page.GetByRole(AriaRole.Radio, new() { Name = "Cash", Exact = true }).CheckAsync();
        var total = await MoneyAsync(Page.GetByTestId("pos-total"));
        total.Should().BeGreaterThan(0, "cash readiness needs a positively priced fixture product");

        await SetMoneyAsync(CashReceived, total - 0.01m);
        await Expect(Pay).ToBeDisabledAsync();
        await Expect(Status).ToContainTextAsync(new Regex("cash|tender|received"), new() { IgnoreCase = true });
        await Expect(Status).ToContainTextAsync(new Regex("enter|insufficient|short|remaining|need"), new() { IgnoreCase = true });
        await ExpectMoneyAsync(Page.GetByTestId("pos-change"), 0);
        var insufficientGuidance = await Status.InnerTextAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await ExpectMoneyInputAsync(CashReceived, total);
        await Expect(Pay).ToBeEnabledAsync();
        await Expect(Status).Not.ToHaveTextAsync(insufficientGuidance);
        await ExpectMoneyAsync(Page.GetByTestId("pos-change"), 0);
        await Expect(Pay).ToHaveTextAsync(new Regex($@"^\s*Pay\s+{Regex.Escape(_settings.Currency)}\s+{Regex.Escape(FormatMoney(total))}\s*$"));

        await SetMoneyAsync(CashReceived, total + 10m);
        await Expect(Pay).ToBeEnabledAsync();
        await ExpectMoneyAsync(Page.GetByTestId("pos-change"), 10m);

        var preset = Page.GetByTestId("pos-cash-preset").Last;
        var presetAmount = await MoneyAsync(preset);
        await preset.ClickAsync();
        await ExpectMoneyInputAsync(CashReceived, presetAmount);
        await ExpectMoneyAsync(Page.GetByTestId("pos-change"), Math.Max(0, presetAmount - total));
        if (presetAmount >= total)
            await Expect(Pay).ToBeEnabledAsync();
        else
            await Expect(Pay).ToBeDisabledAsync();
    }

    [Test]
    public async Task Wallet_NoCustomer_DisablesPaymentWithCustomerGuidance()
    {
        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        await Page.GetByRole(AriaRole.Radio, new() { Name = "Customer wallet", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Radio, new() { Name = "Customer wallet", Exact = true })).ToBeCheckedAsync();
        await Expect(Page.Locator("button.xf-entity-picker-trigger[aria-label='Customer']")).ToBeVisibleAsync();
        await Expect(Pay).ToBeDisabledAsync();
        await Expect(Status).ToContainTextAsync(new Regex("customer"), new() { IgnoreCase = true });
        await Expect(CashReceived).ToBeHiddenAsync();

        await Page.GetByRole(AriaRole.Radio, new() { Name = "Cash", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Radio, new() { Name = "Cash", Exact = true })).ToBeCheckedAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Pay).ToBeEnabledAsync();
        await ExpectQuantityAsync(_settings.ProductName, 1);
    }

    [Test]
    public async Task Adjustments_DiscountAndTax_RecalculateTotalsAndRejectNegativeTotal()
    {
        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        var subtotal = await MoneyAsync(Page.GetByTestId("pos-total"));
        subtotal.Should().BeGreaterThan(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Discount and tax", Exact = true }).ClickAsync();
        var discountInput = Page.GetByLabel($"Discount amount ({_settings.Currency})", new() { Exact = true });
        var taxInput = Page.GetByLabel($"Tax amount ({_settings.Currency})", new() { Exact = true });
        var discount = Math.Round(subtotal / 2, 2, MidpointRounding.AwayFromZero);
        await SetMoneyAsync(discountInput, discount);
        await SetMoneyAsync(taxInput, 0.25m);
        await ExpectMoneyAsync(Page.GetByTestId("pos-subtotal"), subtotal);
        await ExpectMoneyAsync(Page.GetByTestId("pos-discount"), discount);
        await Expect(Page.GetByTestId("pos-discount")).ToContainTextAsync("-");
        await ExpectMoneyAsync(Page.GetByTestId("pos-tax"), 0.25m);
        await ExpectMoneyAsync(Page.GetByTestId("pos-total"), subtotal - discount + 0.25m);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Pay).ToBeEnabledAsync();

        await SetMoneyAsync(discountInput, subtotal + 1m);
        await ExpectMoneyAsync(Page.GetByTestId("pos-total"), -0.75m);
        await Expect(Pay).ToBeDisabledAsync();
        await Expect(Status).ToContainTextAsync(new Regex("discount|negative|total"), new() { IgnoreCase = true });
    }

    [Test]
    public async Task Dialogs_RepeatedOpenAndClose_PreserveDraftWithoutPersistingIt()
    {
        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        var total = await Page.GetByTestId("pos-total").InnerTextAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Hold sale", Exact = true })).ToBeEnabledAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var detailsTrigger = Page.GetByRole(AriaRole.Button, new() { Name = "Sale details", Exact = true });
            await detailsTrigger.ClickAsync();
            var details = Page.GetByRole(AriaRole.Dialog, new() { Name = "Sale details", Exact = true });
            await Expect(details).ToBeVisibleAsync();
            var note = details.GetByLabel("Sale notes", new() { Exact = true });
            if (attempt == 0)
            {
                await note.FillAsync("POS E2E unsaved draft note");
                await note.PressAsync("Tab");
            }
            else
                await Expect(note).ToHaveValueAsync("POS E2E unsaved draft note");

            if (attempt == 1)
                await Page.Keyboard.PressAsync("Escape");
            else
                await details.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true }).ClickAsync();
            await Expect(details).ToBeHiddenAsync();
            await Expect(detailsTrigger).ToBeFocusedAsync();

            var heldTrigger = Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^Held sales(?:\s+\d+)?$") });
            await heldTrigger.ClickAsync();
            var held = Page.GetByRole(AriaRole.Dialog, new() { Name = "Held sales", Exact = true });
            await Expect(held).ToBeVisibleAsync();
            await Page.WaitForFunctionAsync("dialog => dialog.contains(document.activeElement)",
                await held.ElementHandleAsync());
            await Page.Keyboard.PressAsync("Escape");
            await Expect(held).ToBeHiddenAsync();
            await Expect(heldTrigger).ToBeFocusedAsync();
            await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
            await ExpectQuantityAsync(_settings.ProductName, 1);
            await Expect(Page.GetByTestId("pos-total")).ToHaveTextAsync(total);
        }
    }

    [TestCase(1920, 1080, "light")]
    [TestCase(1920, 1080, "dark")]
    [TestCase(1366, 768, "light")]
    [TestCase(1366, 768, "dark")]
    [TestCase(768, 1024, "light")]
    [TestCase(768, 1024, "dark")]
    [TestCase(390, 844, "light")]
    [TestCase(390, 844, "dark")]
    public async Task Layout_ViewportAndFocusMode_ContainContentAndKeepPaymentReachable(int width, int height, string theme)
    {
        Page.ViewportSize.Should().BeEquivalentTo(new { Width = width, Height = height });
        (await Page.Locator("html").EvaluateAsync<bool>("root => root.classList.contains('dark')"))
            .Should().Be(theme == "dark");
        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        await AddProductAsync(_settings.VariantQuery, _settings.VariantName);
        await SearchAsync("");

        for (var pass = 0; pass < 2; pass++)
        {
            await AssertNoOverflowAsync();
            await Pay.ScrollIntoViewIfNeededAsync();
            await Expect(Pay).ToBeInViewportAsync(new() { Ratio = 1 });
            await Expect(Status).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
            await Expect(Pay).ToBeEnabledAsync();
            // Trial click checks reachability/overlays without submitting a financial action.
            await Pay.ClickAsync(new() { Trial = true });
            if (pass == 0)
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = "Enter cashier focus", Exact = true }).ClickAsync();
                await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Exit cashier focus", Exact = true })).ToBeVisibleAsync();
            }
        }

        await Page.GetByRole(AriaRole.Button, new() { Name = "Exit cashier focus", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Enter cashier focus", Exact = true })).ToBeVisibleAsync();
        await ExpectQuantityAsync(_settings.ProductName, 1);
        await ExpectQuantityAsync(_settings.VariantName, 1);
        await AssertNoOverflowAsync();
    }

    [Test]
    public async Task Layout_LongCatalogAndCart_ScrollIndependentlyWithPaymentVisible()
    {
        await Page.SetViewportSizeAsync(1100, 500);
        var available = Page.Locator(".pos-product-tile:not([disabled])");
        (await available.CountAsync()).Should().BeGreaterThanOrEqualTo(_settings.ScrollItemCount,
            "the scroll fixture needs enough distinct sellable entries on the first catalog page; "
            + "see POS_E2E_SCROLL_ITEM_COUNT in POS-E2E.md");
        for (var index = 0; index < _settings.ScrollItemCount; index++)
            await available.Nth(index).ClickAsync();
        await Expect(Lines).ToHaveCountAsync(_settings.ScrollItemCount);

        var catalog = Page.GetByTestId("pos-catalog-scroll");
        var cart = Page.GetByTestId("pos-cart-scroll");
        await AssertIndependentScrollAsync(cart, catalog);
        await AssertIndependentScrollAsync(catalog, cart);
        await Expect(Pay).ToBeInViewportAsync(new() { Ratio = 1 });
        await AssertNoOverflowAsync();
    }

    [Test]
    [Explicit("Creates a real sale and stock/wallet postings. Requires the isolated-data environment guard in POS-E2E.md.")]
    [Category("FinancialMutation")]
    public async Task Checkout_IsolatedCashSale_CompletesAndShowsReceipt()
    {
        Assert.That(PosCashierE2ESettings.Optional("ALLOW_CHECKOUT"), Is.EqualTo("1"),
            "Set POS_E2E_ALLOW_CHECKOUT=1 only for an authorized isolated tenant.");
        Assert.That(PosCashierE2ESettings.Optional("ISOLATED_TENANT_ID"), Is.EqualTo(_settings.TenantId),
            "POS_E2E_ISOLATED_TENANT_ID must exactly match POS_E2E_TENANT_ID; no checkout was submitted.");

        await AddProductAsync(_settings.ProductName, _settings.ProductName);
        var total = await MoneyAsync(Page.GetByTestId("pos-total"));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Exact amount", Exact = true }).ClickAsync();
        await Expect(Pay).ToBeEnabledAsync();
        await Pay.ClickAsync();

        var receipt = Page.GetByTestId("pos-receipt");
        await Expect(receipt).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(receipt).ToContainTextAsync(new Regex("completed"), new() { IgnoreCase = true });
        await Expect(receipt).ToContainTextAsync(FormatMoney(total));
        await Expect(receipt).Not.ToContainTextAsync(new Regex("recovery needed|failed"), new() { IgnoreCase = true });
        TestContext.Progress.WriteLine($"Completed isolated POS sale: {await receipt.InnerTextAsync()}");
        await Expect(Lines).ToHaveCountAsync(0);
        await Expect(Pay).ToBeDisabledAsync();
    }

    private async Task SearchAsync(string query)
    {
        await Search.FillAsync(query);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
        await WaitForCatalogAsync(query);
    }

    private async Task WaitForCatalogAsync(string query)
    {
        await Expect(Catalog).ToHaveAttributeAsync("data-search-query", query, new() { Timeout = 30_000 });
        await WaitForCatalogIdleAsync();
    }

    private async Task WaitForCatalogIdleAsync() =>
        await Expect(Page.Locator("[data-testid='pos-catalog']:not([aria-busy]), [data-testid='pos-catalog'][aria-busy='false']"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

    private async Task ScanAsync(string sku)
    {
        await Search.FillAsync(sku);
        await Search.PressAsync("Enter");
        await Expect(Search).ToHaveValueAsync("", new() { Timeout = 30_000 });
        await WaitForCatalogIdleAsync();
    }

    private ILocator ProductTile(string name) => Page.GetByRole(AriaRole.Button, new() { Name = $"Add {name}", Exact = true });

    private ILocator CartLine(string name) => Lines.Filter(new()
    {
        Has = Page.GetByTestId("pos-cart-item-name").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") })
    });

    private async Task AddProductAsync(string query, string name)
    {
        await SearchAsync(query);
        var tile = ProductTile(name);
        await Expect(tile).ToBeEnabledAsync();
        var unitPrice = await MoneyAsync(tile.Locator(".pos-product-price"));
        await tile.ClickAsync();
        await ExpectQuantityAsync(name, 1);
        await ExpectMoneyAsync(CartLine(name).Locator(".pos-cart-line-main > span"), unitPrice);
    }

    private async Task ExpectQuantityAsync(string name, int quantity) =>
        await Expect(CartLine(name).Locator(".pos-qty-value")).ToHaveTextAsync(quantity.ToString(CultureInfo.InvariantCulture));

    private async Task AssertCartTotalsAsync()
    {
        decimal subtotal = 0;
        foreach (var line in await Lines.AllAsync())
        {
            var unit = await MoneyAsync(line.Locator(".pos-cart-line-main > span"));
            var quantity = decimal.Parse(await line.Locator(".pos-qty-value").InnerTextAsync(), CultureInfo.InvariantCulture);
            var expected = unit * quantity;
            await ExpectMoneyAsync(line.Locator(".pos-cart-line-total"), expected);
            subtotal += expected;
        }

        await ExpectMoneyAsync(Page.GetByTestId("pos-total"), subtotal);
    }

    private static async Task SetMoneyAsync(ILocator input, decimal value)
    {
        await input.FillAsync(value.ToString("0.00", CultureInfo.InvariantCulture));
        await input.PressAsync("Tab");
    }

    private static string FormatMoney(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    private static async Task<decimal> MoneyAsync(ILocator locator)
    {
        await Assertions.Expect(locator).ToBeVisibleAsync();
        var text = await locator.InnerTextAsync();
        var match = Regex.Match(text, @"-?\d[\d,]*\.\d{2}(?!\d)");
        Assert.That(match.Success, Is.True, $"Expected a two-decimal money amount in '{text}'.");
        return decimal.Parse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static async Task ExpectMoneyAsync(ILocator locator, decimal amount) =>
        await Assertions.Expect(locator).ToContainTextAsync(new Regex($@"(?<![\d.,]){Regex.Escape(FormatMoney(amount))}(?![\d.,])"));

    private static async Task ExpectMoneyInputAsync(ILocator locator, decimal amount) =>
        await Assertions.Expect(locator).ToHaveValueAsync(new Regex($@"^(?:{Regex.Escape(FormatMoney(amount))}|{Regex.Escape(amount.ToString("0.00", CultureInfo.InvariantCulture))})$"));

    private static string NoMatchQuery() => $"pos-e2e-no-match-{Guid.NewGuid():N}";

    private async Task AssertHealthyAsync()
    {
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        await Expect(Page.Locator("#components-reconnect-modal")).ToBeHiddenAsync();
        _pageErrors.Select(Redact).Should().BeEmpty("the cashier must not throw browser errors");
        _consoleErrors.Select(Redact).Should().BeEmpty("console errors can reveal a terminated Blazor circuit");
    }

    private async Task AssertNoOverflowAsync()
    {
        var violations = await Page.EvaluateAsync<string[]>("""
            () => {
                const issues = [];
                const tolerance = 2;
                const root = document.documentElement;
                if (root.scrollWidth > root.clientWidth + tolerance)
                    issues.push(`Document horizontal overflow: ${root.scrollWidth} > ${root.clientWidth}`);
                const check = (element, owner, vertical) => {
                    const rect = element.getBoundingClientRect();
                    const bounds = owner.getBoundingClientRect();
                    const label = `${element.className}: ${element.textContent.trim().slice(0, 90)}`;
                    if (rect.width === 0 || rect.height === 0) return;
                    if (rect.left < bounds.left - tolerance || rect.right > bounds.right + tolerance ||
                        (vertical && (rect.top < bounds.top - tolerance || rect.bottom > bounds.bottom + tolerance)))
                        issues.push(`Outside container: ${label}`);
                    if (element.scrollWidth > element.clientWidth + tolerance)
                        issues.push(`Clipped horizontal content: ${label}`);
                };
                for (const tile of document.querySelectorAll('.pos-product-tile')) {
                    check(tile, tile.parentElement, false);
                    for (const child of tile.querySelectorAll('.pos-product-name, .pos-product-variant, .pos-product-meta, .pos-product-price, .pos-product-status'))
                        check(child, tile, true);
                }
                for (const line of document.querySelectorAll('.pos-cart-line')) {
                    check(line, line.parentElement, false);
                    for (const child of line.querySelectorAll('.pos-cart-line-main, .pos-qty-stepper, .pos-cart-line-total, button'))
                        check(child, line, true);
                }
                return issues;
            }
            """);
        violations.Should().BeEmpty("product labels, badges, and cart controls must remain inside their containers");
    }

    private async Task AssertIndependentScrollAsync(ILocator moving, ILocator stationary)
    {
        var stationaryTop = await stationary.EvaluateAsync<double>("element => element.scrollTop");
        var outerScroll = await Page.EvaluateAsync<double[]>("[window.scrollY, document.querySelector('.app-main')?.scrollTop ?? 0]");
        var maximum = await moving.EvaluateAsync<double>("element => element.scrollHeight - element.clientHeight");
        maximum.Should().BeGreaterThan(0, "seed enough distinct catalog/cart entries to exercise natural pane overflow");
        (await moving.EvaluateAsync<string>("element => getComputedStyle(element).overflowY"))
            .Should().BeOneOf("auto", "scroll");
        await moving.EvaluateAsync("element => element.scrollTop = 0");
        await moving.FocusAsync();
        await moving.PressAsync("End");
        await Page.WaitForFunctionAsync("element => Math.abs(element.scrollTop - (element.scrollHeight - element.clientHeight)) < 1", await moving.ElementHandleAsync());
        (await stationary.EvaluateAsync<double>("element => element.scrollTop")).Should().Be(stationaryTop);
        (await Page.EvaluateAsync<double[]>("[window.scrollY, document.querySelector('.app-main')?.scrollTop ?? 0]"))
            .Should().Equal(outerScroll);
    }

    private async Task SaveFailureArtifactsAsync(Exception? browserFailure)
    {
        var name = Regex.Replace(TestContext.CurrentContext.Test.Name, "[^A-Za-z0-9._-]", "_");
        name = name[..Math.Min(name.Length, 64)];
        var directory = Path.Combine(PosCashierE2ESettings.Optional("ARTIFACTS_DIR")
                                     ?? Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", "pos-cashier"),
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{name}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(directory);
        var log = Path.Combine(directory, "browser.txt");
        await File.WriteAllTextAsync(log, Redact($"URL: {Page.Url}\nTest: {TestContext.CurrentContext.Test.FullName}\n"
            + $"Failure: {TestContext.CurrentContext.Result.Message}\n{browserFailure?.Message}\n"
            + $"Page errors:\n{string.Join('\n', _pageErrors)}\nConsole errors:\n{string.Join('\n', _consoleErrors)}\n"
            + $"Failed HTTP responses:\n{string.Join('\n', _httpErrors)}"));
        TestContext.AddTestAttachment(log, "POS browser failure and console errors");

        try
        {
            var screenshot = Path.Combine(directory, "page.png");
            await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true, Timeout = 10_000 });
            TestContext.AddTestAttachment(screenshot, "POS failure screenshot");
            var dom = Path.Combine(directory, "page.html");
            await File.WriteAllTextAsync(dom, Redact(await Page.ContentAsync()));
            TestContext.AddTestAttachment(dom, "Rendered DOM at failure (treat as sensitive)");
        }
        finally
        {
            if (_tracing)
            {
                var trace = Path.Combine(directory, "trace.zip");
                await Context.Tracing.StopAsync(new() { Path = trace });
                TestContext.AddTestAttachment(trace, "Playwright trace (treat as sensitive)");
            }
        }
    }

    private string Redact(string value)
    {
        foreach (var secret in new[] { _settings?.Username, _settings?.Password })
            if (!string.IsNullOrEmpty(secret))
                value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        return value;
    }

    private static (int Width, int Height, bool Dark) TestViewport() =>
        TestContext.CurrentContext.Test.Arguments is [int width, int height, string theme]
            ? (width, height, theme == "dark") : (1366, 768, false);
}
