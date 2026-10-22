using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Api.Features.Reports.GetSalesReport;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Enums;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
public sealed partial class PosReportingTests
{
    [TestCase(1, 1, 1, true)]
    [TestCase(366, 100000, 100, true)]
    [TestCase(367, 1, 25, false)]
    [TestCase(0, 1, 25, false)]
    [TestCase(-1, 1, 25, false)]
    [TestCase(1, 0, 25, false)]
    [TestCase(1, 100001, 25, false)]
    [TestCase(1, 1, 101, false)]
    [TestCase(1, 1, 0, false)]
    public void Validator_EnforcesDateAndPagingBounds(int days, int page, int size, bool valid)
    {
        var request = Request() with { ToUtcExclusive = Request().FromUtc.AddDays(days), Page = page, PageSize = size };
        new GetPosSalesReportValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Test]
    public void Validator_RejectsNonUtcTimesEmptyIdsAndUnknownStatus()
    {
        var validator = new GetPosSalesReportValidator();
        validator.Validate(Request() with { FromUtc = DateTime.SpecifyKind(Request().FromUtc, DateTimeKind.Unspecified) }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { FromUtc = Request().FromUtc.AddHours(1) }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { RegisterId = Guid.Empty }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { CurrencyId = Guid.Empty }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { Status = (PosSaleStatus)999 }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { PaymentMethod = (PosPaymentMethod)999 }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { RefundPage = 0 }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { HourUtc = 24 }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { HourUtc = 4, ToUtcExclusive = Request().FromUtc.AddDays(2) }).IsValid.Should().BeFalse();
        validator.Validate(Request() with { HourUtc = 4 }).IsValid.Should().BeTrue();
    }

    [TestCase("anonymous", 401)]
    [TestCase("expired", 401)]
    [TestCase("no-capability", 403)]
    [TestCase("sales-only", 403)]
    [TestCase("wrong-tenant", 403)]
    [TestCase("unmanaged-tenant", 403)]
    [TestCase("disabled", 403)]
    [TestCase("invalid-range", 400)]
    public async Task Service_RejectsUnauthorizedOrInvalidScopeBeforeDatabaseAccess(string scenario, int expected)
    {
        var tenant = Guid.NewGuid();
        var actor = new TrustedActorIdentity(Guid.NewGuid(), Guid.NewGuid(), tenant, Guid.NewGuid(), new HashSet<string>(),
            new HashSet<string>(scenario == "no-capability" ? [] :
                scenario == "sales-only" ? [PosAuthorizationCapabilities.SalesView] : [PosAuthorizationCapabilities.ReportingView]),
            "tests", DateTimeOffset.UtcNow.AddMinutes(scenario == "expired" ? -10 : 10));
        var accessor = new Accessor { Current = new(scenario == "anonymous" ? null : actor, null,
            scenario == "unmanaged-tenant" ? Guid.NewGuid() : tenant, null, Guid.NewGuid()) };
        var features = new Features(scenario != "disabled");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options,
            new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));
        var service = new PosReportingService(db, new PosRequestContextResolver(accessor), accessor, features, NullLogger<PosReportingService>.Instance);
        var request = Request();
        request.Metadata.RequestedTenantId = scenario == "wrong-tenant" ? Guid.NewGuid() : accessor.Current.EffectiveTenantId;
        if (scenario == "invalid-range") request.PageSize = int.MaxValue;
        var result = await service.GetSalesReportAsync(request, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(expected);
        if (scenario is not ("disabled" or "invalid-range")) features.Calls.Should().Be(0);
    }

    [Test]
    public void Endpoint_BothTransportsRequireReportingViewAndManagedTenantDelegation()
    {
        var method = typeof(GetPosSalesReportEndpoint).GetMethod("Handle")!;
        var bolt = method.GetCustomAttribute<BoltHandlerAttribute>()!;
        var rest = method.GetCustomAttribute<MapEndpointAttribute>()!;
        bolt.AllowAnonymous.Should().BeFalse();
        rest.AllowAnonymous.Should().BeFalse();
        rest.RequireAuthorization.Should().BeTrue();
        bolt.RequiredActorCapabilities.Should().Contain(PosAuthorizationCapabilities.ReportingView);
        rest.RequiredActorCapabilities.Should().Contain(PosAuthorizationCapabilities.ReportingView);
        bolt.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        rest.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        bolt.RequiredCrossTenantActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
        rest.RequiredCrossTenantActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
    }

    [TestCase(false, true, false, true, 400)]
    [TestCase(false, true, false, false, 403)]
    [TestCase(true, true, true, true, 400)]
    [TestCase(true, true, true, false, 403)]
    [TestCase(true, true, false, true, 403)]
    [TestCase(true, false, true, true, 403)]
    [TestCase(false, false, true, true, 403)]
    public async Task Service_ReportingAccess_RequiresPermissionDelegationAndSelectedTenantFeature(
        bool delegated, bool canReport, bool canManageTenants, bool targetFeatureEnabled, int expectedStatus)
    {
        var homeTenant = Guid.NewGuid();
        var targetTenant = delegated ? Guid.NewGuid() : homeTenant;
        var capabilities = new HashSet<string>();
        if (canReport) capabilities.Add(PosAuthorizationCapabilities.ReportingView);
        if (canManageTenants) capabilities.Add(XFrameworkActorCapabilities.IdentityTenantsManage);
        var actor = new TrustedActorIdentity(Guid.NewGuid(), Guid.NewGuid(), homeTenant, Guid.NewGuid(),
            new HashSet<string> { "SuperAdmin" }, capabilities, "tests", DateTimeOffset.UtcNow.AddMinutes(10));
        var accessor = new Accessor { Current = new(actor, null, targetTenant, null, Guid.NewGuid()) };
        var features = new Features(targetFeatureEnabled);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options,
            new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(targetTenant));
        var service = new PosReportingService(db, new PosRequestContextResolver(accessor), accessor, features,
            NullLogger<PosReportingService>.Instance);
        // Invalid paging stops authorized requests after the feature gate, before any database access.
        var request = Request() with { PageSize = int.MaxValue };
        request.Metadata.RequestedTenantId = targetTenant;

        var result = await service.GetSalesReportAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(expectedStatus);
        result.Data.Should().BeNull();
        var reachesFeatureGate = canReport && (!delegated || canManageTenants);
        features.Calls.Should().Be(reachesFeatureGate ? 1 : 0);
        if (reachesFeatureGate)
        {
            features.LastTenantId.Should().Be(targetTenant);
            if (!targetFeatureEnabled) result.Message.Should().Be("Disabled");
        }
    }

    private static GetPosSalesReportRequest Request() => new()
    {
        FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ToUtcExclusive = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };
    private sealed class Accessor : ITrustedInvocationContextAccessor { public TrustedInvocationContext? Current { get; init; } }
    private sealed class Features(bool enabled) : ITenantModuleFeatureService
    {
        public int Calls { get; private set; }
        public Guid? LastTenantId { get; private set; }
        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default)
        {
            Calls++;
            LastTenantId = tenantId;
            moduleKey.Should().Be("pos"); subFeatureKey.Should().Be("reporting");
            return Task.FromResult(enabled ? Result.Success() : Result.Forbidden("Disabled"));
        }
        public Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) => throw new NotSupportedException();
        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null) => throw new NotSupportedException();
    }
}
