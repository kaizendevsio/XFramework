using System.Reflection;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using POS.Domain.Shared.Contracts;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture, Category("Area:PortalContract"), Category("Module:POS")]
public sealed class PosSaleReceiptPrintingTests
{
    [Test]
    public async Task PrintReceipt_Ready_InvokesIsolatedModuleWithCustomerDocument()
    {
        var sale = PosReceiptTests.Sale();
        var js = new ReceiptJs();
        using var page = CreatePage(sale.TenantId, js);
        SetField(page, "_sale", sale);
        SetField(page, "_receiptSettings", new PosReceiptSettings { PaperWidthMm = 58 });
        SetField(page, "_currencyCode", "USD");

        await Print(page);

        js.Imports.Should().Be(1);
        js.Module.Calls.Should().Be(1);
        js.Module.Identifier.Should().Be("printReceipt");
        js.Module.Arguments![0].Should().BeOfType<string>().Which.Should().Contain("SALE-001").And.NotContain("AUDIT-SECRET");
        js.Module.Arguments[1].Should().Be(58);
    }

    [TestCase("loading")]
    [TestCase("missing-settings")]
    [TestCase("wrong-tenant")]
    [TestCase("already-printing")]
    public async Task PrintReceipt_NotReady_DoesNotInvokeJavaScript(string reason)
    {
        var sale = PosReceiptTests.Sale();
        var js = new ReceiptJs();
        using var page = CreatePage(reason == "wrong-tenant" ? Guid.NewGuid() : sale.TenantId, js);
        SetField(page, "_sale", sale);
        if (reason != "missing-settings") SetField(page, "_receiptSettings", new PosReceiptSettings());
        SetField(page, "_loading", reason == "loading");
        SetField(page, "_printing", reason == "already-printing");

        await Print(page);

        js.Imports.Should().Be(0);
        js.Module.Calls.Should().Be(0);
    }

    [Test]
    public async Task PrintReceipt_TenantChangesDuringImport_DoesNotPrintPreviousTenant()
    {
        var sale = PosReceiptTests.Sale();
        var tenantId = sale.TenantId;
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(() => tenantId);
        var js = new ReceiptJs { PendingImport = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var page = CreatePage(tenantId, js);
        SetProperty(page, "TenantFilter", tenant.Object);
        SetField(page, "_sale", sale);
        SetField(page, "_receiptSettings", new PosReceiptSettings());
        var printing = Print(page);
        tenantId = Guid.NewGuid();
        js.PendingImport.SetResult(js.Module);
        await printing;
        js.Module.Calls.Should().Be(0);
    }

    private static SaleDetail CreatePage(Guid tenantId, ReceiptJs js)
    {
        var page = new SaleDetail();
        var tenant = new Mock<IPortalTenantContext>();
        tenant.SetupGet(x => x.SelectedTenantId).Returns(tenantId);
        SetProperty(page, "TenantFilter", tenant.Object);
        SetProperty(page, "ModuleNavigation", Mock.Of<IPortalModuleAvailability>());
        SetProperty(page, "JSRuntime", js);
        SetField(page, "_loading", false);
        return page;
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void SetField(SaleDetail page, string name, object value) => typeof(SaleDetail).GetField(name, Private)!.SetValue(page, value);
    private static void SetProperty(SaleDetail page, string name, object value) => typeof(SaleDetail).GetProperty(name, Private)!.SetValue(page, value);
    private static Task Print(SaleDetail page) => (Task)typeof(SaleDetail).GetMethod("PrintReceipt", Private)!.Invoke(page, null)!;

    private sealed class ReceiptJs : IJSRuntime
    {
        public ReceiptModule Module { get; } = new();
        public TaskCompletionSource<IJSObjectReference>? PendingImport { get; init; }
        public int Imports { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            identifier.Should().Be("import", "printing must never call the global page print function");
            args![0].Should().Be("./_content/XFramework.Portal.Features.POS/js/receipt-print.js");
            Imports++;
            return (TValue)(object)(PendingImport is null ? Module : await PendingImport.Task);
        }
    }

    private sealed class ReceiptModule : IJSObjectReference
    {
        public int Calls { get; private set; }
        public string? Identifier { get; private set; }
        public object?[]? Arguments { get; private set; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        { Calls++; Identifier = identifier; Arguments = args; return ValueTask.FromResult(default(TValue)!); }
    }
}
