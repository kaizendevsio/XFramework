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
using XFramework.Domain.Contexts;

namespace Attendance.Tests.Services;

public sealed class AttendanceServiceTests
{
    [Test]
    public async Task RecordEventAsync_CheckInAndCheckOut_ReturnsPresentRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context);

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
        checkOut.Data.Record.FirstCheckInAt.Should().Be(seed.Session.StartsAt);
        checkOut.Data.Record.LastCheckOutAt.Should().Be(seed.Session.EndsAt);
    }

    [Test]
    public async Task RecordEventAsync_DuplicateIdempotencyKey_ReturnsExistingEvent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAttendanceAsync(database);
        var service = CreateService(database.Context);
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
        var service = CreateService(database.Context);

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
        var service = CreateService(database.Context);

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
        var service = CreateService(database.Context);

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
        var service = CreateService(database.Context);

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
        var service = CreateService(database.Context);

        var result = await service.CreateAdjustmentAsync(new CreateAttendanceAdjustmentRequest
        {
            TenantId = database.TenantId,
            SessionId = seed.Session.Id,
            ParticipantId = seed.Participant.Id,
            NewStatus = AttendanceRecordStatus.ManualAdjusted,
            ActorCredentialId = Guid.NewGuid(),
            Reason = "Scanner was offline",
            Notes = "Corrected from paper log"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NewStatus.Should().Be(AttendanceRecordStatus.ManualAdjusted);
        result.Data.Record!.Status.Should().Be(AttendanceRecordStatus.ManualAdjusted);
        result.Data.Reason.Should().Be("Scanner was offline");
    }

    [Test]
    public async Task CreateContextAsync_MissingTenant_ReturnsFailure()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Context);

        var result = await service.CreateContextAsync(new CreateAttendanceContextRequest
        {
            Name = "No tenant",
            ContextType = AttendanceContextType.General
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Tenant ID");
    }

    private static AttendanceService CreateService(AppDbContext db) =>
        new(db, NullLogger<AttendanceService>.Instance);

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

        var participant = new AttendanceParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = database.TenantId,
            ContextId = context.Id,
            CredentialId = Guid.NewGuid(),
            DisplayName = "Student One",
            StartedAt = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        var startsAt = DateTime.UtcNow.AddMinutes(-5);
        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = database.TenantId,
            ContextId = context.Id,
            Name = "Morning class",
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(1),
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

    private sealed record SeedData(
        AttendanceContext Context,
        AttendanceParticipant Participant,
        AttendanceSession Session);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, AppDbContext context, Guid tenantId)
        {
            this.connection = connection;
            Context = context;
            TenantId = tenantId;
        }

        public AppDbContext Context { get; }
        public Guid TenantId { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("now", () => DateTime.UtcNow);
            connection.CreateFunction("uuid_generate_v4", () => Guid.NewGuid());

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenant:DefaultId"] = tenantId.ToString()
                })
                .Build();

            var context = new AppDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor(), configuration);

            _ = typeof(AttendanceContext).Assembly;
            await context.Database.EnsureCreatedAsync();

            return new TestDatabase(connection, context, tenantId);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}

