using Audit.Api.Services;
using Audit.Api.Features.Events.Search;
using Audit.Domain.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;

namespace Audit.IntegrationTests;

[TestFixture]
public sealed class AuditViewerTests
{
    private PostgreSqlContainer postgres = null!;
    private AppDbContext db = null!;
    private readonly Guid tenant = Guid.NewGuid(), other = Guid.NewGuid();
    private Mock<ITrustedInvocationContextAccessor> context = null!;
    private Mock<ITenantModuleFeatureService> gate = null!;
    private AuditQueryService service = null!;
    private long eventId;
    [OneTimeSetUp]
    public async Task Setup()
    {
        postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)).Options);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS "Inventario";
            DROP TABLE IF EXISTS "Inventario"."Product" CASCADE;
            CREATE TABLE "Inventario"."Product" ("Id" uuid primary key, "TenantId" uuid, "Name" text, "SecretToken" text);
            SELECT audit.ensure_table_triggers();
            """);
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Inventario"."Product" VALUES ({id}, {tenant}, 'before', 'never expose');
            """);
        eventId = await db.Set<XFramework.Domain.Auditing.AuditEvent>().MaxAsync(e => e.EventId);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Inventario"."Product" SET "TenantId"={other}, "Name"='other tenant secret' WHERE "Id"={id};
            """);
    }
    [SetUp]
    public void Reset()
    {
        context = new();
        context.SetupGet(x => x.Current).Returns(Invocation(tenant, ["audit:view"]));
        gate = new();
        gate.Setup(x => x.EnsureEnabledAsync(tenant, "audit", null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        service = new AuditQueryService(db, context.Object, gate.Object);
    }
    private static TrustedInvocationContext Invocation(Guid tenant, string[] capabilities) => new(
        new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(), capabilities.ToHashSet(), "test", DateTimeOffset.UtcNow.AddHours(1)),
        null, tenant, tenant, Guid.NewGuid());
    [OneTimeTearDown]
    public async Task Cleanup() { if (db is not null) await db.DisposeAsync(); if (postgres is not null) await postgres.DisposeAsync(); }
    [Test]
    public async Task Search_ValidTenant_ProjectsOnlyAuthorizedRows()
    {
        var result = await service.SearchAsync(new() { Table = "Product", To = DateTimeOffset.UtcNow.AddMinutes(1) }, default);
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.Last().EventId.Should().Be(eventId);
        result.Data.Items.First().ChangedFields.Should().Be("Restricted");
    }
    [Test]
    public async Task Get_Transfer_HidesOtherTenantSnapshot()
    {
        var result = await service.GetAsync(new() { EventId = eventId + 1 }, default);
        result.IsSuccess.Should().BeTrue();
        result.Data!.After.Should().BeNull();
        result.Data.Before.Should().Contain("[REDACTED]").And.NotContain("never expose");
        result.Data.Fields.Should().OnlyContain(f => f.After == "Restricted");
        System.Text.Json.JsonSerializer.Serialize(result.Data).Should().NotContain("other tenant secret");
    }
    [Test]
    public async Task Get_WrongTenant_ReturnsNotFound()
    {
        var unrelated = Guid.NewGuid();
        context.SetupGet(x => x.Current).Returns(Invocation(unrelated, ["audit:view"]));
        gate.Setup(x => x.EnsureEnabledAsync(unrelated, "audit", null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        (await service.GetAsync(new() { EventId = eventId }, default)).StatusCode.Should().Be(404);
    }
    [Test]
    public async Task Search_MissingPermissionOrActor_FailsClosed()
    {
        context.SetupGet(x => x.Current).Returns(Invocation(tenant, []));
        (await service.SearchAsync(new(), default)).StatusCode.Should().Be(403);
        context.SetupGet(x => x.Current).Returns((TrustedInvocationContext?)null);
        (await service.SearchAsync(new(), default)).StatusCode.Should().Be(403);
    }
    [Test]
    public async Task Get_DisabledModule_DeniesHistory()
    {
        gate.Setup(x => x.EnsureEnabledAsync(tenant, "audit", null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Forbidden("Disabled"));
        (await service.GetAsync(new() { EventId = eventId }, default)).StatusCode.Should().Be(403);
    }
    [Test]
    public async Task Search_NativeFiltersAndPagination_ExecuteInPostgres()
    {
        var result = await service.SearchAsync(new()
        {
            Count = 1, Filters = [new() { Field = "Table", Operator = "Equals", Value = "Product" }],
            To = DateTimeOffset.UtcNow.AddMinutes(1), OldestFirst = true
        }, default);
        result.Data!.TotalItemCount.Should().Be(2);
        result.Data.Items.Single().EventId.Should().Be(eventId);
    }
    [Test]
    public async Task Search_RelatedAnchor_CannotExpandTenantScope()
    {
        var result = await service.SearchAsync(new() { AnchorEventId = eventId, RelatedMode = "record", To = DateTimeOffset.UtcNow.AddMinutes(1) }, default);
        result.Data!.Items.Should().HaveCount(2);
        (await service.SearchAsync(new() { AnchorEventId = long.MaxValue, RelatedMode = "record" }, default)).StatusCode.Should().Be(404);
    }
    [Test]
    public async Task Search_RelatedHistory_FilterDoesNotExcludeAnchor()
    {
        var result = await service.SearchAsync(new()
        {
            AnchorEventId = eventId, RelatedMode = "record", To = DateTimeOffset.UtcNow.AddMinutes(1),
            Filters = [new() { Field = "Action", Operator = "Equals", Value = "update" }]
        }, default);
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.EventId.Should().Be(eventId + 1);
    }
    [Test]
    public void Validate_UnboundedOrUnsupportedQuery_IsRejected()
    {
        var validator = new SearchAuditEventsValidator();
        validator.Validate(new SearchAuditEventsRequest { Count = 101, From = DateTimeOffset.UtcNow.AddYears(-1), Filters = [new() { Field = "OldValues" }] }).IsValid.Should().BeFalse();
        validator.Validate(new SearchAuditEventsRequest { EntityKey = "not json" }).IsValid.Should().BeFalse();
    }

    [Test]
    public async Task Presentation_UnreviewedSource_DoesNotReturnPayload()
    {
        var row = await db.Set<XFramework.Domain.Auditing.AuditEvent>().AsNoTracking().SingleAsync(e => e.EventId == eventId);
        db.Entry(row).Property(e => e.SchemaName).CurrentValue = "Identity";
        var detail = AuditPresentation.ToDetail(row, tenant);
        detail.Before.Should().BeNull();
        detail.After.Should().BeNull();
        detail.Fields.Should().BeEmpty();
        db.Entry(row).State = EntityState.Detached;
    }

    [Test]
    public void Presentation_CompositeKey_HidesNonIdComponents()
    {
        AuditPresentation.SafeKey("""{"ID":"visible","TenantId":"other-tenant","SecretToken":"hidden"}""")
            .Should().Contain("visible").And.NotContain("other-tenant").And.NotContain("hidden");
    }

    [Test]
    public void Endpoints_RequireTrustedActorCapabilityAndServiceScope()
    {
        foreach (var type in new[] {
            typeof(Audit.Api.Features.Events.Search.SearchAuditEventsEndpoint),
            typeof(Audit.Api.Features.Events.Get.GetAuditEventsEndpoint) })
        {
            var policy = (XFramework.Integration.Attributes.BoltHandlerAttribute)Attribute.GetCustomAttribute(
                type.GetMethod("Handle")!, typeof(XFramework.Integration.Attributes.BoltHandlerAttribute))!;
            policy.RequiredActorCapabilities.Should().Contain("audit:view");
            policy.RequiredServiceScopes.Should().Contain("audit.read");
            policy.RequiredCrossTenantActorCapabilities.Should().Contain("identity.tenants:manage");
            policy.AllowAnonymous.Should().BeFalse();
            policy.ActorRequirement.Should().Be(ActorRequirement.Required);
        }
    }
}
