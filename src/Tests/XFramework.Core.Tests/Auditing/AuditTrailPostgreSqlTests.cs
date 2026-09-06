using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Domain.Auditing;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Security;

namespace XFramework.Core.Tests.Auditing;

[TestFixture]
public sealed class AuditTrailPostgreSqlTests
{
    private PostgreSqlContainer? postgres;
    private string connectionString = null!;
    private TestAuditContextAccessor auditContext = null!;
    private DbContextOptions<AppDbContext> options = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
            postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("xframework_audit_test")
                .WithUsername("audit_test_owner")
                .WithPassword("audit_test_password")
                .Build();
            await postgres.StartAsync();
        }
        catch (Exception exception) when (
            exception is DockerUnavailableException
            || exception is ArgumentException
               && exception.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("Audit integration tests require a Testcontainers-compatible Docker endpoint.");
        }

        connectionString = postgres!.GetConnectionString();
        auditContext = new TestAuditContextAccessor
        {
            ActorCredentialId = Guid.NewGuid(),
            ActorIdentityId = Guid.NewGuid(),
            ActorTenantId = Guid.NewGuid(),
            EffectiveTenantId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            ServiceClientId = "audit-integration-tests"
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        httpContext.Request.Headers.UserAgent = "XFramework audit integration test";
        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(), "Audit test mutation"));

        var interceptor = new AuditContextConnectionInterceptor(
            [auditContext],
            new HttpContextAccessor { HttpContext = httpContext });

        options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA audit_test;
            CREATE TABLE audit_test.widget
            (
                "Id" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "Name" text NOT NULL,
                "SecretToken" text NULL,
                "Payload" bytea NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            SELECT audit.ensure_table_triggers();
            """);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (postgres is not null)
        {
            await postgres.DisposeAsync();
        }
    }

    [Test]
    public async Task Trigger_CapturesOrderedAttributedRedactedChangesInTheBusinessTransaction()
    {
        var id = Guid.NewGuid();
        var tenantId = auditContext.EffectiveTenantId!.Value;
        byte[] binaryPayload = [1, 2, 3, 4];

        using var activity = new Activity("audit-test").Start();
        await using (var db = new AppDbContext(options))
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO audit_test.widget ("Id", "TenantId", "Name", "SecretToken", "Payload")
                VALUES ({id}, {tenantId}, {"Before"}, {"must-not-be-recorded"}, {binaryPayload});
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE audit_test.widget SET "Name" = {"After"}, "IsDeleted" = true WHERE "Id" = {id};
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_test.widget WHERE \"Id\" = {id};");
            await transaction.CommitAsync();
        }

        var events = await ReadEvents(id);
        events.Should().HaveCount(3);
        events.Select(item => item.Operation).Should().Equal("INSERT", "UPDATE", "DELETE");
        events.Select(item => item.TransactionOrdinal).Should().Equal(1, 2, 3);
        events.Select(item => item.TransactionId).Distinct().Should().ContainSingle();
        events[1].EventKind.Should().Be("soft_delete");
        events.Should().OnlyContain(item => item.ActorCredentialId == auditContext.ActorCredentialId);
        events.Should().OnlyContain(item => item.EffectiveTenantId == tenantId);
        events.Should().OnlyContain(item => item.CorrelationId == auditContext.CorrelationId);
        events.Should().OnlyContain(item => item.ServiceName == auditContext.ServiceClientId);
        events.Should().OnlyContain(item => item.SubjectTenantIdAfter == tenantId || item.Operation == "DELETE");
        events[0].TraceId.Should().Be(activity.TraceId.ToHexString());

        using var insertedValues = JsonDocument.Parse(events[0].NewValues!);
        insertedValues.RootElement.GetProperty("SecretToken").GetString().Should().Be("[REDACTED]");
        insertedValues.RootElement.TryGetProperty("Payload", out _).Should().BeFalse();
        events[1].ChangedFields.Should().Contain(["Name", "IsDeleted"]);
        events[1].OldValues.Should().Contain("Before");
        events[1].NewValues.Should().Contain("After");
    }

    [Test]
    public async Task Rollback_RemovesTheBusinessMutationAndItsAuditEvent()
    {
        var id = Guid.NewGuid();
        await using (var db = new AppDbContext(options))
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO audit_test.widget ("Id", "TenantId", "Name")
                VALUES ({id}, {auditContext.EffectiveTenantId}, {"Rolled back"});
                """);
            await transaction.RollbackAsync();
        }

        (await ReadEvents(id)).Should().BeEmpty();
    }

    [Test]
    public async Task CaptureFailure_FailsClosedAndPreventsTheBusinessMutation()
    {
        var id = Guid.NewGuid();
        await using (var db = new AppDbContext(options))
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE audit.event ADD CONSTRAINT audit_test_reject_new_events CHECK (false) NOT VALID;");

            var mutation = () => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO audit_test.widget ("Id", "TenantId", "Name")
                VALUES ({id}, {auditContext.EffectiveTenantId}, {"Must fail closed"});
                """);
            await mutation.Should().ThrowAsync<PostgresException>();
            await transaction.RollbackAsync();
        }

        await using var verification = new NpgsqlConnection(connectionString);
        await verification.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM audit_test.widget WHERE \"Id\" = @id;",
            verification);
        command.Parameters.AddWithValue("id", id);
        Convert.ToInt64(await command.ExecuteScalarAsync()).Should().Be(0);
        (await ReadEvents(id)).Should().BeEmpty();
    }

    [Test]
    public async Task AuditEvent_RejectsUpdateDeleteTruncateAndDirectInsert()
    {
        await using var db = new AppDbContext(options);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO audit_test.widget ("Id", "TenantId", "Name")
            VALUES ({Guid.NewGuid()}, {auditContext.EffectiveTenantId}, {"Immutability probe"});
            """);

        foreach (var statement in new[]
                 {
                     "UPDATE audit.event SET actor_kind = 'Forged' WHERE event_id = (SELECT max(event_id) FROM audit.event);",
                     "DELETE FROM audit.event WHERE event_id = (SELECT max(event_id) FROM audit.event);",
                     "TRUNCATE audit.event;",
                     """
                     INSERT INTO audit.event
                         (event_kind, operation, transaction_id, transaction_ordinal, database_user,
                          actor_kind, schema_name, table_name, entity_key, changed_fields)
                     VALUES ('insert', 'INSERT', 1, 1, current_user, 'Forged', 'x', 'x', '{{}}'::jsonb, ARRAY[]::text[]);
                     """
                 })
        {
            var mutation = () => db.Database.ExecuteSqlRawAsync(statement);
            var exception = await mutation.Should().ThrowAsync<PostgresException>();
            exception.Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
        }
    }

    [Test]
    public async Task CoverageCheck_ReportsNoEligibleTableWithoutTheAuditTrigger()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT * FROM audit.missing_table_triggers();", connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeFalse();
    }

    private async Task<List<AuditEvent>> ReadEvents(Guid id)
    {
        await using var db = new AppDbContext(options);
        var candidates = await db.AuditEvents
            .AsNoTracking()
            .Where(item => item.SchemaName == "audit_test" && item.TableName == "widget")
            .OrderBy(item => item.EventId)
            .ToListAsync();

        return candidates
            .Where(item => JsonDocument.Parse(item.EntityKey).RootElement.GetProperty("Id").GetGuid() == id)
            .ToList();
    }

    private sealed class TestAuditContextAccessor : IAuditContextAccessor
    {
        public bool HasAuditContext => true;
        public Guid? ActorCredentialId { get; init; }
        public Guid? ActorIdentityId { get; init; }
        public Guid? ActorTenantId { get; init; }
        public Guid? EffectiveTenantId { get; init; }
        public Guid? SessionId { get; init; }
        public string? ServiceClientId { get; init; }
        public Guid? CorrelationId { get; init; }
    }
}
