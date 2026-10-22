using System.Reflection;
using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Portal.Features.Finance.Pages;

namespace Portal.E2ETests;

[TestFixture]
public sealed class WalletCaseFormTests
{
    [TestCase("")]
    [TestCase("invalid")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public async Task SubmitCase_MissingOriginalTransaction_StopsBeforeBackendCall(string transactionId)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddBlazorBlueprintComponents();
        using var provider = services.BuildServiceProvider();
        var page = new RefundsDisputes();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(RefundsDisputes).GetProperty("ToastService", flags)!.SetValue(page, provider.GetRequiredService<ToastService>());
        typeof(RefundsDisputes).GetField("_walletId", flags)!.SetValue(page, Guid.NewGuid().ToString());
        typeof(RefundsDisputes).GetField("_amount", flags)!.SetValue(page, 1m);
        typeof(RefundsDisputes).GetField("_transactionId", flags)!.SetValue(page, transactionId);

        // No backend or logger is injected: submission must return at form validation.
        await (Task)typeof(RefundsDisputes).GetMethod("SubmitCase", flags)!.Invoke(page, null)!;

        typeof(RefundsDisputes).GetField("_acting", flags)!.GetValue(page).Should().Be(false);
    }
}
