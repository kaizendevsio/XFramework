using Attendance.Api.Services;
using Attendance.Domain.Shared.Contracts;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Enums;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;

namespace Attendance.Tests.Services;

[NonParallelizable]
public sealed class AttendanceServiceTests
{
    [Test]
    public async Task RecordEventAsync_CheckInAndCheckOut_ReturnsPresentRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var checkIn = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            IdempotencyKey = "present-check-in"
        }, CancellationToken.None);

        var checkOut = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckOut,
            OccurredAt = seed.Session.EndsAt,
            IdempotencyKey = "present-check-out"
        }, CancellationToken.None);

        checkIn.IsSuccess.Should().BeTrue();
        checkOut.IsSuccess.Should().BeTrue();
        checkOut.Data!.Record!.Status.Should().Be(AttendanceRecordStatus.Present);
        checkOut.Data.Record.FirstCheckInAt.Should().BeCloseTo(seed.Session.StartsAt, TimeSpan.FromMicroseconds(1));
        checkOut.Data.Record.LastCheckOutAt.Should().BeCloseTo(seed.Session.EndsAt, TimeSpan.FromMicroseconds(1));
    }

    [Test]
    public async Task RecordEventAsync_DuplicateIdempotencyKey_ReturnsExistingEvent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);
        var request = new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            IdempotencyKey = "replay-key"
        };

        var first = await service.RecordEventAsync(request, CancellationToken.None);
        var replay = await service.RecordEventAsync(request, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Data!.Id.Should().Be(first.Data!.Id);
        replay.Message.Should().Contain("replayed");
    }

    [Test]
    public async Task GetRecordAsync_NoEvent_ReturnsAbsentRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var result = await service.GetRecordAsync(new GetAttendanceRecordRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Id.Should().BeNull();
        result.Data.Status.Should().Be(AttendanceRecordStatus.Absent);
    }

    [Test]
    public async Task RecordEventAsync_LateCheckIn_ReturnsLateRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt.AddMinutes(10),
            IdempotencyKey = "late-check-in"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Record!.Status.Should().Be(AttendanceRecordStatus.Late);
    }

    [Test]
    public async Task RecordEventAsync_OnTimeCheckInWithoutCheckout_ReturnsIncompleteRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            IdempotencyKey = "incomplete-check-in"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Record!.Status.Should().Be(AttendanceRecordStatus.Incomplete);
    }

    [Test]
    public async Task RecordEventAsync_CheckOutBeforeCheckIn_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckOut,
            OccurredAt = seed.Session.EndsAt,
            IdempotencyKey = "checkout-first"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("check in");
    }

    [Test]
    public async Task CreateAdjustmentAsync_ManualAdjustedStatus_UpdatesRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var result = await service.CreateAdjustmentAsync(new CreateAttendanceAdjustmentRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            NewStatus = AttendanceRecordStatus.ManualAdjusted,
            Reason = "Scanner was offline",
            Notes = "Corrected from paper log"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NewStatus.Should().Be(AttendanceRecordStatus.ManualAdjusted);
        result.Data.Record!.Status.Should().Be(AttendanceRecordStatus.ManualAdjusted);
        result.Data.ActorCredentialId.Should().Be(database.ActorCredentialId);
        result.Data.Reason.Should().Be("Scanner was offline");
    }

    [Test]
    public async Task RecordEventAsync_SpoofedActorCredential_ReturnsForbidden()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            RecordedByCredentialId = Guid.NewGuid(),
            IdempotencyKey = "spoofed-event-actor"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await database.Context.Set<AttendanceEvent>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RecordEventAsync_NoSuppliedActor_PersistsTrustedActor()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            IdempotencyKey = "trusted-event-actor"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.RecordedByCredentialId.Should().Be(database.ActorCredentialId);
    }

    [Test]
    public async Task CreateAdjustmentAsync_SpoofedActorCredential_ReturnsForbidden()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var result = await service.CreateAdjustmentAsync(new CreateAttendanceAdjustmentRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            NewStatus = AttendanceRecordStatus.Excused,
            ActorCredentialId = Guid.NewGuid(),
            Reason = "Approved absence"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await database.Context.Set<AttendanceAdjustment>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RemoveParticipantAsync_Deactivation_PreservesMembershipHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);
        var endedAt = seed.Session.StartsAt.AddMinutes(30);

        var result = await service.RemoveParticipantAsync(new RemoveAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ParticipantId = seed.Participant.Id,
            EndedAt = endedAt
        }, CancellationToken.None);

        database.Context.ChangeTracker.Clear();
        var participant = await database.Context.Set<AttendanceParticipant>()
            .SingleAsync(item => item.Id == seed.Participant.Id);
        result.IsSuccess.Should().BeTrue();
        participant.IsActive.Should().BeFalse();
        participant.IsDeleted.Should().BeFalse();
        participant.EndedAt.Should().Be(new DateTime(endedAt.Ticks - endedAt.Ticks % 10, DateTimeKind.Utc));
    }

    [Test]
    public async Task GetReportAsync_MembershipChangedLater_UsesSessionEffectiveRoster()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);
        var endedAt = seed.Session.StartsAt.AddMinutes(30);

        await service.RemoveParticipantAsync(new RemoveAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ParticipantId = seed.Participant.Id,
            EndedAt = endedAt
        }, CancellationToken.None);

        database.Context.Set<AttendanceParticipant>().AddRange(
            CreateParticipant(database.TenantId, seed.Context.Id, seed.Session.EndsAt.AddMinutes(1)),
            CreateParticipant(database.TenantId, seed.Context.Id, seed.Session.EndsAt.AddMinutes(1)));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var result = await service.GetReportAsync(new GetAttendanceReportRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            FromUtc = seed.Session.StartsAt.AddMinutes(-1),
            ToUtc = seed.Session.EndsAt.AddMinutes(2)
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ActiveParticipantCount.Should().Be(2);
        result.Data.Sessions.Should().ContainSingle();
        result.Data.Sessions[0].AbsentCount.Should().Be(1);
    }

    [Test]
    public async Task RecordEventAsync_SameIdempotencyKeyDifferentPayload_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var first = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            IdempotencyKey = "payload-aware-key"
        }, CancellationToken.None);
        var conflictingReplay = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckOut,
            OccurredAt = seed.Session.EndsAt,
            IdempotencyKey = "payload-aware-key"
        }, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        conflictingReplay.IsSuccess.Should().BeFalse();
        conflictingReplay.StatusCode.Should().Be(409);
        (await database.Context.Set<AttendanceEvent>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task CreateSessionAsync_InvalidTimeZone_ReturnsValidationFailure()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        var result = await service.CreateSessionAsync(new CreateAttendanceSessionRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            Name = "Invalid time zone",
            StartsAt = DateTime.UtcNow.AddHours(1),
            EndsAt = DateTime.UtcNow.AddHours(2),
            TimeZoneId = "Not/A-TimeZone",
            Status = AttendanceSessionStatus.Scheduled
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("time zone");
    }

    [Test]
    public async Task RecordEventAsync_ScheduledSession_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var scheduledSession = await database.Context.Set<AttendanceSession>()
            .AsTracking()
            .SingleAsync(item => item.Id == seed.Session.Id);
        scheduledSession.Status = AttendanceSessionStatus.Scheduled;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var service = CreateService(database.Context, database.TenantId);

        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt,
            IdempotencyKey = "scheduled-session-event"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("open sessions");
    }

    [Test]
    public async Task RecordEventAsync_CheckoutBeforeRecordedCheckIn_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId);

        await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckIn,
            OccurredAt = seed.Session.StartsAt.AddMinutes(10),
            IdempotencyKey = "chronology-check-in"
        }, CancellationToken.None);
        var result = await service.RecordEventAsync(new RecordAttendanceEventRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            EventType = AttendanceEventType.CheckOut,
            OccurredAt = seed.Session.StartsAt.AddMinutes(5),
            IdempotencyKey = "chronology-check-out"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("before check-in");
    }

    [Test]
    public async Task CreateAdjustmentAsync_CheckoutBeforeCheckIn_ReturnsValidationFailure()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var result = await service.CreateAdjustmentAsync(new CreateAttendanceAdjustmentRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            NewStatus = AttendanceRecordStatus.ManualAdjusted,
            AdjustedCheckInAt = seed.Session.StartsAt.AddMinutes(10),
            AdjustedCheckOutAt = seed.Session.StartsAt.AddMinutes(5),
            Reason = "Invalid chronology"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("before adjusted check-in");
    }

    [Test]
    public async Task TransitionSessionAsync_ScheduledToOpenToClosed_Succeeds()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var persistedSession = await database.Context.Set<AttendanceSession>()
            .AsTracking()
            .SingleAsync(item => item.Id == seed.Session.Id);
        persistedSession.Status = AttendanceSessionStatus.Scheduled;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var opened = await service.TransitionSessionAsync(new TransitionAttendanceSessionRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            Status = AttendanceSessionStatus.Open
        }, CancellationToken.None);
        var closed = await service.TransitionSessionAsync(new TransitionAttendanceSessionRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            Status = AttendanceSessionStatus.Closed
        }, CancellationToken.None);

        opened.IsSuccess.Should().BeTrue();
        opened.Data!.Status.Should().Be(AttendanceSessionStatus.Open);
        closed.IsSuccess.Should().BeTrue();
        closed.Data!.Status.Should().Be(AttendanceSessionStatus.Closed);
    }

    [Test]
    public async Task TransitionSessionAsync_InvalidTransition_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var persistedSession = await database.Context.Set<AttendanceSession>()
            .AsTracking()
            .SingleAsync(item => item.Id == seed.Session.Id);
        persistedSession.Status = AttendanceSessionStatus.Scheduled;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var service = CreateService(database.Context, database.TenantId, database.ActorCredentialId);

        var result = await service.TransitionSessionAsync(new TransitionAttendanceSessionRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            Status = AttendanceSessionStatus.Closed
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("Scheduled").And.Contain("Closed");
    }

    [Test]
    public async Task AddParticipantAsync_CredentialDoesNotExist_ReturnsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var credentialId = Guid.NewGuid();
        var service = CreateService(
            database.Context,
            database.TenantId,
            credentialResolver: new TestCredentialResolver(
                Result<AttendanceCredentialSnapshot>.NotFound("Identity credential was not found")));

        var result = await service.AddParticipantAsync(new AddAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            CredentialId = credentialId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task AddParticipantAsync_IdentityServerUnavailable_ReturnsServiceUnavailable()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(
            database.Context,
            database.TenantId,
            credentialResolver: new TestCredentialResolver(
                Result<AttendanceCredentialSnapshot>.Failure("IdentityServer is unavailable", 503)));

        var result = await service.AddParticipantAsync(new AddAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            CredentialId = Guid.NewGuid()
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Message.Should().Contain("unavailable");
    }

    [Test]
    public async Task AddParticipantAsync_CredentialBelongsToAnotherTenant_ReturnsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var credentialId = Guid.NewGuid();
        var credential = new AttendanceCredentialSnapshot(
            credentialId,
            Guid.NewGuid(),
            true,
            false,
            "Wrong tenant",
            "wrong-tenant");
        var service = CreateService(
            database.Context,
            database.TenantId,
            credentialResolver: new TestCredentialResolver(Result<AttendanceCredentialSnapshot>.Success(credential)));

        var result = await service.AddParticipantAsync(new AddAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            CredentialId = credentialId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task AddParticipantAsync_DisabledCredential_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var credentialId = Guid.NewGuid();
        var credential = new AttendanceCredentialSnapshot(
            credentialId,
            database.TenantId,
            false,
            false,
            "Disabled",
            "disabled");
        var service = CreateService(
            database.Context,
            database.TenantId,
            credentialResolver: new TestCredentialResolver(Result<AttendanceCredentialSnapshot>.Success(credential)));

        var result = await service.AddParticipantAsync(new AddAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            CredentialId = credentialId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task AddParticipantAsync_DeletedCredential_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var credentialId = Guid.NewGuid();
        var credential = new AttendanceCredentialSnapshot(
            credentialId,
            database.TenantId,
            true,
            true,
            "Deleted",
            "deleted");
        var service = CreateService(
            database.Context,
            database.TenantId,
            credentialResolver: new TestCredentialResolver(Result<AttendanceCredentialSnapshot>.Success(credential)));

        var result = await service.AddParticipantAsync(new AddAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            CredentialId = credentialId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task AddParticipantAsync_ActiveCredential_CopiesAuthoritativeLabels()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var credentialId = Guid.NewGuid();
        var credential = new AttendanceCredentialSnapshot(
            credentialId,
            database.TenantId,
            true,
            false,
            "Authoritative alias",
            "authoritative.user");
        var service = CreateService(
            database.Context,
            database.TenantId,
            credentialResolver: new TestCredentialResolver(Result<AttendanceCredentialSnapshot>.Success(credential)));

        var result = await service.AddParticipantAsync(new AddAttendanceParticipantRequest
        {
            TenantId = database.TenantId,
            ContextId = seed.Context.Id,
            CredentialId = credentialId,
            DisplayName = "Caller supplied name",
            ReferenceCode = "caller-supplied-code"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.DisplayName.Should().Be("Authoritative alias");
        result.Data.ReferenceCode.Should().Be("authoritative.user");
    }

    [Test]
    public async Task CreateContextAsync_MissingTenant_ReturnsFailure()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Context, null);

        var result = await service.CreateContextAsync(new CreateAttendanceContextRequest
        {
            Name = "No tenant",
            ContextType = AttendanceContextType.General
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Tenant ID");
    }

    private static AttendanceService CreateService(
        AppDbContext db,
        Guid? tenantId,
        Guid? actorCredentialId = null,
        IAttendanceCredentialResolver? credentialResolver = null) =>
        new(
            db,
            NullLogger<AttendanceService>.Instance,
            new TestInvocationContextAccessor(tenantId, actorCredentialId ?? Guid.NewGuid()),
            credentialResolver ?? new TestCredentialResolver(
                Result<AttendanceCredentialSnapshot>.NotFound("Identity credential was not found")));

    private sealed class TestInvocationContextAccessor(Guid? tenantId, Guid actorCredentialId)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current { get; } = tenantId is { } value
            ? new TrustedInvocationContext(
                new TrustedActorIdentity(
                    actorCredentialId,
                    Guid.NewGuid(),
                    value,
                    Guid.NewGuid(),
                    new HashSet<string>(),
                    new HashSet<string>(),
                    "test",
                    DateTimeOffset.UtcNow.AddMinutes(5)),
                null,
                value,
                null,
                Guid.NewGuid())
            : null;
    }

    private sealed class TestCredentialResolver(Result<AttendanceCredentialSnapshot> result)
        : IAttendanceCredentialResolver
    {
        public Task<Result<AttendanceCredentialSnapshot>> ResolveAsync(
            Guid credentialId,
            Guid tenantId,
            CancellationToken ct) => Task.FromResult(result);
    }

    private static async Task<SeedData> SeedAttendanceAsync(TestDatabase database)
    {
        var context = new AttendanceContext
        {
            Id = Guid.NewGuid(),
            TenantId = database.TenantId,
            Name = "Grade 10",
            Code = "G10",
            ContextType = AttendanceContextType.School,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        var startsAt = DateTime.UtcNow.AddHours(-1);
        var participant = new AttendanceParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = database.TenantId,
            ContextId = context.Id,
            CredentialId = Guid.NewGuid(),
            DisplayName = "Student One",
            StartedAt = startsAt.AddHours(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = database.TenantId,
            ContextId = context.Id,
            Name = "Morning class",
            StartsAt = startsAt,
            EndsAt = DateTime.UtcNow,
            TimeZoneId = "UTC",
            Status = AttendanceSessionStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        database.Context.Set<AttendanceContext>().Add(context);
        database.Context.Set<AttendanceParticipant>().Add(participant);
        database.Context.Set<AttendanceSession>().Add(session);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        return new SeedData(context, participant, session);
    }

    private static AttendanceParticipant CreateParticipant(Guid tenantId, Guid contextId, DateTime startedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ContextId = contextId,
        CredentialId = Guid.NewGuid(),
        DisplayName = "Later participant",
        StartedAt = startedAt,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };

    private sealed record SeedData(
        AttendanceContext Context,
        AttendanceParticipant Participant,
        AttendanceSession Session);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(
            SqliteConnection connection,
            AppDbContext context,
            Guid tenantId,
            Guid actorCredentialId)
        {
            this.connection = connection;
            Context = context;
            TenantId = tenantId;
            ActorCredentialId = actorCredentialId;
        }

        public AppDbContext Context { get; }
        public Guid TenantId { get; }
        public Guid ActorCredentialId { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var actorCredentialId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("now", () => DateTime.UtcNow);
            connection.CreateFunction("uuid_generate_v4", () => Guid.NewGuid());

            var options = new DbContextOptionsBuilder<AttendanceTestDbContext>()
                .UseSqlite(connection)
                .Options;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenant:DefaultId"] = tenantId.ToString()
                })
                .Build();

            var context = new AttendanceTestDbContext(
                options,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(),
                configuration,
                new TestEffectiveTenantContextAccessor(tenantId));

            _ = typeof(AttendanceContext).Assembly;
            await context.Database.EnsureCreatedAsync();

            return new TestDatabase(connection, context, tenantId, actorCredentialId);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }

        private sealed class TestEffectiveTenantContextAccessor(Guid tenantId)
            : XFramework.Domain.Shared.Security.IEffectiveTenantContextAccessor
        {
            public bool HasTrustedInvocation => true;
            public Guid? EffectiveTenantId => tenantId;
        }

        private sealed class AttendanceTestDbContext(
            DbContextOptions<AttendanceTestDbContext> options,
            Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            XFramework.Domain.Shared.Security.IEffectiveTenantContextAccessor effectiveTenantContextAccessor)
            : AppDbContext(options, httpContextAccessor, configuration, effectiveTenantContextAccessor)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                var attendanceAssembly = typeof(AttendanceContext).Assembly;
                var unrelatedEntityTypes = modelBuilder.Model.GetEntityTypes()
                    .Where(entityType => entityType.ClrType.Assembly != attendanceAssembly)
                    .Select(entityType => entityType.ClrType)
                    .ToArray();

                foreach (var entityType in unrelatedEntityTypes)
                {
                    modelBuilder.Ignore(entityType);
                }
            }
        }
    }
}

