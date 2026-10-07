using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Inventario.Api.Features.Setup.Complete;
using Inventario.Api.Features.Setup.UpdatePreferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Enums;

namespace InventarioOnboarding.IntegrationTests;

[TestFixture]
[Category("Kind:Unit"), Category("Module:Inventario"), Category("Area:Setup")]
public sealed class InventarioSetupValidationTests
{
    [TestCase(InventarioSetupMode.Basic, 0, " php ")]
    [TestCase(InventarioSetupMode.Advanced, 5, "USD")]
    [TestCase(InventarioSetupMode.Advanced, int.MaxValue, "SGD")]
    public async Task Validators_AcceptExistingScalarContract(InventarioSetupMode mode, int threshold, string currency)
    {
        var complete = new CompleteInventarioSetupRequest
        {
            CompletionRequestId = Guid.NewGuid(), Mode = mode, LowStockThreshold = threshold, DefaultCurrency = currency
        };
        (await new CompleteInventarioSetupValidator().ValidateAsync(complete)).IsValid.Should().BeTrue();
        (await new UpdateInventarioPreferencesValidator().ValidateAsync(new UpdateInventarioPreferencesRequest
        {
            LowStockThreshold = threshold, DefaultCurrency = currency
        })).IsValid.Should().BeTrue();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Complete_InvalidIdOrMode_IsRejectedBeforeDatabaseAccess(bool emptyId)
    {
        var request = new CompleteInventarioSetupRequest
        {
            CompletionRequestId = emptyId ? Guid.Empty : Guid.NewGuid(),
            Mode = emptyId ? InventarioSetupMode.Basic : (InventarioSetupMode)999
        };
        var validation = await new CompleteInventarioSetupValidator().ValidateAsync(request);
        validation.Errors.Select(x => x.PropertyName).Should().Equal(emptyId ? "CompletionRequestId" : "Mode");
        await using var db = new DbContext(new DbContextOptions<DbContext>());
        var result = await Service(db).CompleteAsync(request);
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be(validation.Errors.Single().ErrorMessage);
    }

    [TestCase(-1, "PHP", "LowStockThreshold")]
    [TestCase(0, null, "DefaultCurrency")]
    [TestCase(0, "", "DefaultCurrency")]
    [TestCase(0, "   ", "DefaultCurrency")]
    [TestCase(0, "ZZZ", "DefaultCurrency")]
    [TestCase(0, "US", "DefaultCurrency")]
    public async Task BothWrites_InvalidPreferences_UseValidatorsBeforeDatabaseAccess(int threshold, string? currency, string property)
    {
        var complete = new CompleteInventarioSetupRequest
        {
            CompletionRequestId = Guid.NewGuid(), LowStockThreshold = threshold, DefaultCurrency = currency!
        };
        var preferences = new UpdateInventarioPreferencesRequest { LowStockThreshold = threshold, DefaultCurrency = currency! };
        var completeValidation = await new CompleteInventarioSetupValidator().ValidateAsync(complete);
        var preferencesValidation = await new UpdateInventarioPreferencesValidator().ValidateAsync(preferences);
        completeValidation.Errors.Select(x => x.PropertyName).Should().Equal(property);
        preferencesValidation.Errors.Select(x => x.PropertyName).Should().Equal(property);

        // A providerless context fails if either write reaches persistence before rejecting input.
        await using var db = new DbContext(new DbContextOptions<DbContext>());
        var service = Service(db);
        var completeResult = await service.CompleteAsync(complete);
        var preferencesResult = await service.UpdatePreferencesAsync(preferences);
        completeResult.StatusCode.Should().Be(400);
        preferencesResult.StatusCode.Should().Be(400);
        completeResult.Message.Should().Be(completeValidation.Errors.Single().ErrorMessage);
        preferencesResult.Message.Should().Be(preferencesValidation.Errors.Single().ErrorMessage);
    }

    [Test]
    public async Task BothWrites_InvalidScalars_DoNotBypassManagementAuthorization()
    {
        await using var db = new DbContext(new DbContextOptions<DbContext>());
        var service = Service(db, manager: false);
        (await service.CompleteAsync(new() { LowStockThreshold = -1 })).StatusCode.Should().Be(403);
        (await service.UpdatePreferencesAsync(new() { LowStockThreshold = -1 })).StatusCode.Should().Be(403);
    }

    [Test]
    public void Validators_AreDiscoveredByExistingAssemblyScanning()
    {
        using var provider = new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<CompleteInventarioSetupValidator>().BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IValidator<CompleteInventarioSetupRequest>>()
            .Should().BeOfType<CompleteInventarioSetupValidator>();
        scope.ServiceProvider.GetRequiredService<IValidator<UpdateInventarioPreferencesRequest>>()
            .Should().BeOfType<UpdateInventarioPreferencesValidator>();
    }

    private static InventarioSetupService Service(DbContext db, bool manager = true) =>
        new(db, new Invocation(manager), new EnabledFeatures(), NullLogger<InventarioSetupService>.Instance);

    private sealed class Invocation(bool manager) : ITrustedInvocationContextAccessor
    {
        private readonly Guid _tenant = Guid.NewGuid();
        public TrustedInvocationContext Current => new(new TrustedActorIdentity(Guid.NewGuid(), null, _tenant, Guid.NewGuid(),
            new HashSet<string>(), manager ? new HashSet<string> { XFrameworkActorCapabilities.IdentityTenantsManage } : new HashSet<string>(),
            "test", DateTimeOffset.UtcNow.AddHours(1)), null, _tenant, null, Guid.NewGuid());
    }

    private sealed class EnabledFeatures : ITenantModuleFeatureService
    {
        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());
        public Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));
        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null) { }
    }
}
