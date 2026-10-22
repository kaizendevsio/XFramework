using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Api.Features.Reports.GetProductSalesReport;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

public sealed partial class PosReportingTests
{
    [TestCase("anonymous", 401)]
    [TestCase("expired", 401)]
    [TestCase("no-capability", 403)]
    [TestCase("wrong-tenant", 403)]
    [TestCase("unmanaged-tenant", 403)]
    [TestCase("disabled", 403)]
    [TestCase("invalid-range", 400)]
    public async Task ProductReport_RejectsUnauthorizedOrInvalidScopeBeforeDatabaseAccess(string scenario, int expected)
    {
        var tenant = Guid.NewGuid();
        var actor = new TrustedActorIdentity(Guid.NewGuid(), Guid.NewGuid(), tenant, Guid.NewGuid(), new HashSet<string>(),
            new HashSet<string>(scenario == "no-capability" ? [] : [PosAuthorizationCapabilities.ReportingView]),
            "tests", DateTimeOffset.UtcNow.AddMinutes(scenario == "expired" ? -10 : 10));
        var accessor = new Accessor { Current = new(scenario == "anonymous" ? null : actor, null,
            scenario == "unmanaged-tenant" ? Guid.NewGuid() : tenant, null, Guid.NewGuid()) };
        var features = new Features(scenario != "disabled");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options,
            new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));
        var service = new PosReportingService(db, new PosRequestContextResolver(accessor), accessor, features, NullLogger<PosReportingService>.Instance);
        var request = new GetPosProductSalesReportRequest();
        request.Metadata.RequestedTenantId = scenario == "wrong-tenant" ? Guid.NewGuid() : accessor.Current.EffectiveTenantId;
        if (scenario == "invalid-range") request.Take = int.MaxValue;
        var result = await service.GetProductSalesReportAsync(request, CancellationToken.None);
        result.StatusCode.Should().Be(expected);
        result.Data.Should().BeNull();
    }

    [Test]
    public void ProductReport_BothTransportsRequireReportingViewAndManagedTenantDelegation()
    {
        var method = typeof(GetPosProductSalesReportEndpoint).GetMethod("Handle")!;
        var bolt = method.GetCustomAttribute<BoltHandlerAttribute>()!;
        var rest = method.GetCustomAttribute<MapEndpointAttribute>()!;
        bolt.RequiredActorCapabilities.Should().Contain(PosAuthorizationCapabilities.ReportingView);
        rest.RequiredActorCapabilities.Should().Contain(PosAuthorizationCapabilities.ReportingView);
        bolt.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        rest.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        bolt.RequiredCrossTenantActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
        rest.RequiredCrossTenantActorCapabilities.Should().Contain(XFrameworkActorCapabilities.IdentityTenantsManage);
    }

    [TestCase("Contains")][TestCase("NotContains")][TestCase("Equals")][TestCase("NotEquals")]
    [TestCase("StartsWith")][TestCase("EndsWith")][TestCase("IsEmpty")][TestCase("IsNotEmpty")]
    public void ProductReport_ValidatorAcceptsBlueprintStringOperators(string operation) =>
        new GetPosProductSalesReportValidator().Validate(new GetPosProductSalesReportRequest { ProductFilterOperator = operation }).IsValid.Should().BeTrue();

    [Test]
    public void ProductReport_ValidatorBoundsDatesPagingAndFilters()
    {
        var validator = new GetPosProductSalesReportValidator();
        var valid = new GetPosProductSalesReportRequest();
        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { Skip = -1 }).IsValid.Should().BeFalse();
        validator.Validate(valid with { Take = 101 }).IsValid.Should().BeFalse();
        validator.Validate(valid with { Take = 0 }).IsValid.Should().BeFalse();
        validator.Validate(valid with { ToUtcExclusive = valid.FromUtc }).IsValid.Should().BeFalse();
        validator.Validate(valid with { ToUtcExclusive = valid.FromUtc.AddDays(367) }).IsValid.Should().BeFalse();
        validator.Validate(valid with { FromUtc = DateTime.SpecifyKind(valid.FromUtc, DateTimeKind.Unspecified) }).IsValid.Should().BeFalse();
        validator.Validate(valid with { ProductFilter = new string('a', 201) }).IsValid.Should().BeFalse();
        validator.Validate(valid with { ProductFilterOperator = "unknown" }).IsValid.Should().BeFalse();
    }
}
