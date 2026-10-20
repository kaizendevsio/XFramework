using System.Text.Json;
using FluentAssertions;
using IdentityServer.Api.Features.FinanceSettings;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class TenantFinanceSettingsTests
{
    private static readonly Guid Usd = Guid.NewGuid();
    private static readonly Guid Sgd = Guid.NewGuid();
    private static readonly TenantFinanceCurrency[] Currencies =
    [new() { Id = Usd, Code = "USD" }, new() { Id = Sgd, Code = "SGD" }];

    [Test]
    public void TryResolve_MissingRegistry_UsesEnabledCatalogWithDeterministicDefault()
    {
        TenantFinanceSettings.TryResolve(null, Currencies, out var settings).Should().BeTrue();
        settings!.DefaultCurrencyId.Should().Be(Sgd);
        settings.SupportedCurrencyIds.Should().BeEquivalentTo([Usd, Sgd]);
        settings.TaxEnabled.Should().BeFalse();
    }

    [Test]
    public void TryResolve_ExplicitRegistry_OverridesFallback()
    {
        var configured = new TenantFinanceSettings { DefaultCurrencyId = Usd, SupportedCurrencyIds = [Usd], TaxEnabled = true, TaxRatePercent = 9 };
        TenantFinanceSettings.TryResolve(JsonSerializer.Serialize(configured), Currencies, out var settings).Should().BeTrue();
        settings!.SupportedCurrencyIds.Should().Equal(Usd);
        settings.DefaultCurrencyId.Should().Be(Usd);
        settings.CalculateTax(100).Should().Be(9);
    }

    [TestCase("")]
    [TestCase("not json")]
    [TestCase("null")]
    [TestCase("{}")]
    public void TryResolve_InvalidExplicitRegistry_DoesNotFallback(string value) =>
        TenantFinanceSettings.TryResolve(value, Currencies, out _).Should().BeFalse();

    [Test]
    public void TryResolve_UnknownOrDisabledCurrency_FailsClosed()
    {
        var settings = new TenantFinanceSettings { DefaultCurrencyId = Usd, SupportedCurrencyIds = [Usd] };
        TenantFinanceSettings.TryResolve(JsonSerializer.Serialize(settings), [Currencies[1]], out _).Should().BeFalse();
        TenantFinanceSettings.TryResolve(null, [], out _).Should().BeFalse();
    }

    [TestCase(-1)]
    [TestCase(101)]
    public void IsValid_InvalidRate_IsRejected(decimal rate) =>
        new TenantFinanceSettings { DefaultCurrencyId = Usd, SupportedCurrencyIds = [Usd], TaxRatePercent = rate }.IsValid().Should().BeFalse();

    [Test]
    public void IsValid_DefaultOutsideSupportedSetAndDuplicateIds_AreRejected()
    {
        new TenantFinanceSettings { DefaultCurrencyId = Sgd, SupportedCurrencyIds = [Usd] }.IsValid().Should().BeFalse();
        new TenantFinanceSettings { DefaultCurrencyId = Usd, SupportedCurrencyIds = [Usd, Usd] }.IsValid().Should().BeFalse();
    }

    [Test]
    public void Endpoints_ReadNeedsNoWalletCapability_WriteRequiresFinanceManagement()
    {
        var read = typeof(GetTenantFinanceSettingsEndpoint).GetMethod("Handle")!.GetCustomAttributes(typeof(BoltHandlerAttribute), false).Cast<BoltHandlerAttribute>().Single();
        var write = typeof(UpdateTenantFinanceSettingsEndpoint).GetMethod("Handle")!.GetCustomAttributes(typeof(BoltHandlerAttribute), false).Cast<BoltHandlerAttribute>().Single();
        read.RequiredActorCapabilities.Should().BeNullOrEmpty();
        write.RequiredActorCapabilities.Should().Contain("wallets:manage");
    }

    [Test]
    public async Task UpdateAsync_WithoutManagementCapability_RejectsBeforeDatabaseAccess()
    {
        var tenant = Guid.NewGuid();
        var actor = new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(), new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddHours(1));
        var accessor = new Mock<ITrustedInvocationContextAccessor>();
        accessor.SetupGet(x => x.Current).Returns(new TrustedInvocationContext(actor, null, tenant, tenant, Guid.NewGuid()));
        var service = new TenantFinanceSettingsService(null!, accessor.Object, NullLogger<TenantFinanceSettingsService>.Instance);
        var result = await service.UpdateAsync(new(), CancellationToken.None);
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task GetAsync_SpoofedTenant_RejectsBeforeDatabaseAccess()
    {
        var tenant = Guid.NewGuid();
        var accessor = new Mock<ITrustedInvocationContextAccessor>();
        accessor.SetupGet(x => x.Current).Returns(new TrustedInvocationContext(null, null, tenant, tenant, Guid.NewGuid()));
        var service = new TenantFinanceSettingsService(null!, accessor.Object, NullLogger<TenantFinanceSettingsService>.Instance);
        var result = await service.GetAsync(new() { Metadata = new() { RequestedTenantId = Guid.NewGuid() } }, CancellationToken.None);
        result.StatusCode.Should().Be(403);
    }
}
