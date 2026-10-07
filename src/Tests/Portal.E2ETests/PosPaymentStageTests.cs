using System.Collections;
using System.Reflection;
using FluentAssertions;
using Moq;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using Wallets.Domain.Shared.Contracts;
using XFramework.Portal.Features.POS.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class PosPaymentStageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Cashier _page = null!;

    [SetUp]
    public void SetUp()
    {
        _page = new();
        var actor = new Mock<IPortalActorContext>();
        actor.SetupGet(x => x.CredentialId).Returns(Guid.NewGuid());
        typeof(Cashier).GetProperty("ActorContext", Private)!.SetValue(_page, actor.Object);
        var register = new PosRegisterResponse { Id = Guid.NewGuid(), CurrencyId = Guid.NewGuid() };
        Set("_registers", new List<PosRegisterResponse> { register });
        Set("_selectedRegisterId", register.Id.ToString());
        Set("_currencies", new List<CurrencyType> { new() { Id = register.CurrencyId, CurrencyIsoCode3 = "USD" } });
        var lineType = typeof(Cashier).GetNestedType("CartLine", BindingFlags.NonPublic)!;
        var line = Activator.CreateInstance(lineType)!;
        lineType.GetProperty("Quantity")!.SetValue(line, 1m);
        lineType.GetProperty("UnitPrice")!.SetValue(line, 10m);
        ((IList)typeof(Cashier).GetField("_cart", Private)!.GetValue(_page)!).Add(line);
    }

    [Test]
    public async Task Pay_WithoutTender_OpensStageWithoutSubmittingPayment()
    {
        Get<bool>("CanBeginPayment").Should().BeTrue();
        Get<bool>("CanCheckout").Should().BeFalse();
        await (Task)typeof(Cashier).GetMethod("BeginPayment", Private)!.Invoke(_page, null)!;
        Field<bool>("_paymentOpen").Should().BeTrue();
        Field<decimal>("_cashTenderedAmount").Should().Be(0);
    }

    [TestCase(0, false)]
    [TestCase(9.99, false)]
    [TestCase(10, true)]
    [TestCase(20, true)]
    public void ConfirmPayment_StillRequiresSufficientTender(decimal tender, bool canPay)
    {
        Set("_cashTenderedAmount", tender);
        Get<bool>("CanCheckout").Should().Be(canPay);
    }

    [Test]
    public async Task InvalidAdjustments_CanReopenPaymentToCorrectButCannotSubmit()
    {
        Set("_discountAmount", 20m);
        Get<bool>("CanCheckout").Should().BeFalse();
        Get<bool>("CanBeginPayment").Should().BeTrue();
        await (Task)typeof(Cashier).GetMethod("BeginPayment", Private)!.Invoke(_page, null)!;
        Field<bool>("_paymentOpen").Should().BeTrue();
        typeof(Cashier).GetMethod("PaymentOpenChanged", Private)!.Invoke(_page, [false]);
        await (Task)typeof(Cashier).GetMethod("BeginPayment", Private)!.Invoke(_page, null)!;
        Field<bool>("_paymentOpen").Should().BeTrue();
        Set("_discountAmount", 0m);
        Set("_cashTenderedAmount", 10m);
        Get<bool>("CanCheckout").Should().BeTrue();
    }

    [Test]
    public void BackToCart_PreservesTenderAndCart()
    {
        Set("_paymentOpen", true);
        Set("_cashTenderedAmount", 20m);
        typeof(Cashier).GetMethod("PaymentOpenChanged", Private)!.Invoke(_page, [false]);
        Field<bool>("_paymentOpen").Should().BeFalse();
        Get<decimal>("Total").Should().Be(10m);
        Field<decimal>("_cashTenderedAmount").Should().Be(20m);
    }

    [Test]
    public void ProcessingPayment_CannotCloseDialogOrStartAnotherPayment()
    {
        Set("_paymentOpen", true);
        Set("_checkingOut", true);
        typeof(Cashier).GetMethod("PaymentOpenChanged", Private)!.Invoke(_page, [false]);
        Field<bool>("_paymentOpen").Should().BeTrue();
        Get<bool>("CanBeginPayment").Should().BeFalse();
        Get<bool>("CanCheckout").Should().BeFalse();
    }

    [TestCase(PosSaleStatus.PaymentFailed, false)]
    [TestCase(PosSaleStatus.PaymentPending, true)]
    public void RecoveryStatus_PreservesExistingRetryEligibility(PosSaleStatus status, bool canRetry)
    {
        Set("_lastReceipt", new PosSaleReceiptResponse { Status = status });
        Get<bool>("CanBeginPayment").Should().Be(canRetry);
        Get<bool>("CanCheckout").Should().Be(canRetry);
    }

    private void Set(string name, object value) => typeof(Cashier).GetField(name, Private)!.SetValue(_page, value);
    private T Field<T>(string name) => (T)typeof(Cashier).GetField(name, Private)!.GetValue(_page)!;
    private T Get<T>(string name) => (T)typeof(Cashier).GetProperty(name, Private)!.GetValue(_page)!;
}
