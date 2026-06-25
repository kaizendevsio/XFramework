using System.Net;
using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using XFramework.Domain.Shared.DataContext;
using XFramework.TestInfrastructure;

namespace Attendance.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.Attendance)]
public sealed class AttendancePostgresTests : AttendanceIntegrationTestBase
{
    [Test]
    [Category(TestCategories.DataContext)]
    public async Task Migration_CreatesAttendanceTablesAndReportIndexes()
    {
        await using var db = CreateDbContext();

        var tableCount = await ScalarAsync<long>(
            db,
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'Attendance'
              AND table_name IN (
                  'AttendanceContext',
                  'AttendanceParticipant',
                  'AttendanceSession',
                  'AttendanceEvent',
                  'AttendanceRecord',
                  'AttendancePolicy',
                  'AttendanceAdjustment'
              );
            """);

        var reportIndexExists = await ScalarAsync<bool>(
            db,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'Attendance'
                  AND indexname = 'IX_AttendanceSession_Tenant_Context_Start'
            );
            """);

        tableCount.Should().Be(7);
        reportIndexExists.Should().BeTrue();
    }

    [Test]
    [Category(TestCategories.Auth)]
    public async Task GetContexts_UnauthenticatedRequest_ReturnsUnauthorizedOrForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/attendance/contexts");
        request.Headers.Add(TestAuthHeaders.Unauthenticated, "true");

        using var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_CreateParticipantSessionEventsAndReport_PersistsAttendance()
    {
        var metadata = CreateMetadata();
        var context = await ServiceWrapper.CreateAttendanceContext(new CreateAttendanceContextRequest
        {
            Metadata = metadata,
            Name = $"Wrapper context {Guid.NewGuid():N}",
            Code = UniqueCode("CTX"),
            ContextType = AttendanceContextType.Project
        });
        context.IsSuccess.Should().BeTrue(context.Message);

        var participant = await ServiceWrapper.AddAttendanceParticipant(new AddAttendanceParticipantRequest
        {
            Metadata = metadata,
            ContextId = context.Response!.Id,
            CredentialId = Guid.NewGuid(),
            DisplayName = "Wrapper Participant",
            ReferenceCode = UniqueCode("P")
        });
        participant.IsSuccess.Should().BeTrue(participant.Message);

        var startsAt = DateTime.UtcNow.AddMinutes(-20);
        var session = await ServiceWrapper.CreateAttendanceSession(new CreateAttendanceSessionRequest
        {
            Metadata = metadata,
            ContextId = context.Response.Id,
            Name = "Wrapper Session",
            Code = UniqueCode("S"),
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(30),
            TimeZoneId = "UTC",
            Status = AttendanceSessionStatus.Open
        });
        session.IsSuccess.Should().BeTrue(session.Message);

        var checkIn = await ServiceWrapper.RecordAttendanceEvent(new RecordAttendanceEventRequest
        {
            Metadata = metadata,
            SessionId = session.Response!.Id,
            ParticipantId = participant.Response!.Id,
            EventType = AttendanceEventType.CheckIn,
            Source = AttendanceEventSource.Api,
            OccurredAt = startsAt,
            IdempotencyKey = UniqueCode("IN")
        });
        checkIn.IsSuccess.Should().BeTrue(checkIn.Message);

        var checkOut = await ServiceWrapper.RecordAttendanceEvent(new RecordAttendanceEventRequest
        {
            Metadata = metadata,
            SessionId = session.Response.Id,
            ParticipantId = participant.Response.Id,
            EventType = AttendanceEventType.CheckOut,
            Source = AttendanceEventSource.Api,
            OccurredAt = startsAt.AddMinutes(30),
            IdempotencyKey = UniqueCode("OUT")
        });
        checkOut.IsSuccess.Should().BeTrue(checkOut.Message);
        checkOut.Response!.Record!.Status.Should().Be(AttendanceRecordStatus.Present);

        var report = await ServiceWrapper.GetAttendanceReport(new GetAttendanceReportRequest
        {
            Metadata = metadata,
            ContextId = context.Response.Id,
            FromUtc = startsAt.AddMinutes(-1),
            ToUtc = startsAt.AddMinutes(1),
            PageSize = 20
        });
        report.IsSuccess.Should().BeTrue(report.Message);
        report.Response!.Sessions.Should().ContainSingle(item =>
            item.SessionId == session.Response.Id &&
            item.PresentCount == 1 &&
            item.AbsentCount == 0);

        await using var db = CreateDbContext();
        var persistedRecord = await db.Set<AttendanceRecord>()
            .IgnoreQueryFilters()
            .SingleAsync(item =>
                item.TenantId == AttendanceIntegrationTestFixture.TestTenantId &&
                item.SessionId == session.Response.Id &&
                item.ParticipantId == participant.Response.Id);
        persistedRecord.Status.Should().Be(AttendanceRecordStatus.Present);
    }

    [Test]
    [Category(TestCategories.Wrappers)]
    public async Task Wrapper_DuplicateIdempotencyKey_ReplaysExistingEvent()
    {
        var metadata = CreateMetadata();
        var seed = await SeedAttendanceAsync(metadata);
        var idempotencyKey = UniqueCode("IDEMP");

        var first = await ServiceWrapper.RecordAttendanceEvent(new RecordAttendanceEventRequest
        {
            Metadata = metadata,
            SessionId = seed.SessionId,
            ParticipantId = seed.ParticipantId,
            EventType = AttendanceEventType.CheckIn,
            Source = AttendanceEventSource.Api,
            OccurredAt = seed.StartsAt,
            IdempotencyKey = idempotencyKey
        });

        var replay = await ServiceWrapper.RecordAttendanceEvent(new RecordAttendanceEventRequest
        {
            Metadata = metadata,
            SessionId = seed.SessionId,
            ParticipantId = seed.ParticipantId,
            EventType = AttendanceEventType.CheckIn,
            Source = AttendanceEventSource.Api,
            OccurredAt = seed.StartsAt,
            IdempotencyKey = idempotencyKey
        });

        first.IsSuccess.Should().BeTrue(first.Message);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Response!.Id.Should().Be(first.Response!.Id);

        await using var db = CreateDbContext();
        var persistedEventCount = await db.Set<AttendanceEvent>()
            .IgnoreQueryFilters()
            .CountAsync(item =>
                item.TenantId == AttendanceIntegrationTestFixture.TestTenantId &&
                item.IdempotencyKey == idempotencyKey);
        persistedEventCount.Should().Be(1);
    }

    [Test]
    [Category(TestCategories.DataContext)]
    public async Task RemoteDataContext_QueryAttendanceContext_ReturnsEntityFromAttendanceService()
    {
        var metadata = CreateMetadata();
        var context = await ServiceWrapper.CreateAttendanceContext(new CreateAttendanceContextRequest
        {
            Metadata = metadata,
            Name = $"Remote context {Guid.NewGuid():N}",
            Code = UniqueCode("REMOTE"),
            ContextType = AttendanceContextType.Event
        });
        context.IsSuccess.Should().BeTrue(context.Message);

        var remoteContexts = await DataContext.Query<AttendanceContext>()
            .Where(item => item.Id == context.Response!.Id)
            .ToListAsync();

        remoteContexts.Should().ContainSingle(item => item.Id == context.Response!.Id);
    }

    private static async Task<SeedAttendance> SeedAttendanceAsync(XFramework.Domain.Shared.BusinessObjects.RequestMetadata metadata)
    {
        var context = await ServiceWrapper.CreateAttendanceContext(new CreateAttendanceContextRequest
        {
            Metadata = metadata,
            Name = $"Seed context {Guid.NewGuid():N}",
            Code = UniqueCode("SEED"),
            ContextType = AttendanceContextType.Project
        });
        context.IsSuccess.Should().BeTrue(context.Message);

        var participant = await ServiceWrapper.AddAttendanceParticipant(new AddAttendanceParticipantRequest
        {
            Metadata = metadata,
            ContextId = context.Response!.Id,
            CredentialId = Guid.NewGuid(),
            DisplayName = "Project member"
        });
        participant.IsSuccess.Should().BeTrue(participant.Message);

        var startsAt = DateTime.UtcNow.AddMinutes(-10);
        var session = await ServiceWrapper.CreateAttendanceSession(new CreateAttendanceSessionRequest
        {
            Metadata = metadata,
            ContextId = context.Response.Id,
            Name = "Standup",
            Code = UniqueCode("STANDUP"),
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(30),
            TimeZoneId = "UTC",
            Status = AttendanceSessionStatus.Open
        });
        session.IsSuccess.Should().BeTrue(session.Message);

        return new SeedAttendance(participant.Response!.Id, session.Response!.Id, startsAt);
    }

    private static async Task<T> ScalarAsync<T>(DbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)value!;
    }

    private sealed record SeedAttendance(Guid ParticipantId, Guid SessionId, DateTime StartsAt);
}
