using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Contracts.Responses;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Attendance.Integration.Drivers;

public interface IAttendanceServiceWrapper : IServiceWrapper, IDataContextServiceWrapper
{
    Task<QueryResponse<AttendanceContextResponse>> CreateAttendanceContext(CreateAttendanceContextRequest request);
    Task<QueryResponse<AttendanceContextResponse>> UpdateAttendanceContext(UpdateAttendanceContextRequest request);
    Task<QueryResponse<GetAttendanceContextsResponse>> GetAttendanceContexts(GetAttendanceContextsRequest request);
    Task<QueryResponse<AttendanceParticipantResponse>> AddAttendanceParticipant(AddAttendanceParticipantRequest request);
    Task<CmdResponse> RemoveAttendanceParticipant(RemoveAttendanceParticipantRequest request);
    Task<QueryResponse<GetAttendanceParticipantsResponse>> GetAttendanceParticipants(GetAttendanceParticipantsRequest request);
    Task<QueryResponse<AttendanceSessionResponse>> CreateAttendanceSession(CreateAttendanceSessionRequest request);
    Task<QueryResponse<GetAttendanceSessionsResponse>> GetAttendanceSessions(GetAttendanceSessionsRequest request);
    Task<QueryResponse<AttendanceEventResponse>> RecordAttendanceEvent(RecordAttendanceEventRequest request);
    Task<QueryResponse<AttendanceRecordResponse>> GetAttendanceRecord(GetAttendanceRecordRequest request);
    Task<QueryResponse<AttendanceReportResponse>> GetAttendanceReport(GetAttendanceReportRequest request);
    Task<QueryResponse<AttendanceAdjustmentResponse>> CreateAttendanceAdjustment(CreateAttendanceAdjustmentRequest request);
}

public sealed record AttendanceServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration,
    BoltClient boltClient
) : DriverBase(messageBusDriver, configuration), IAttendanceServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "Attendance".ToSha256();
    }

    public Task<QueryResponse<AttendanceContextResponse>> CreateAttendanceContext(CreateAttendanceContextRequest request) =>
        SendAsync<CreateAttendanceContextRequest, AttendanceContextResponse>(request);

    public Task<QueryResponse<AttendanceContextResponse>> UpdateAttendanceContext(UpdateAttendanceContextRequest request) =>
        SendAsync<UpdateAttendanceContextRequest, AttendanceContextResponse>(request);

    public Task<QueryResponse<GetAttendanceContextsResponse>> GetAttendanceContexts(GetAttendanceContextsRequest request) =>
        SendAsync<GetAttendanceContextsRequest, GetAttendanceContextsResponse>(request);

    public Task<QueryResponse<AttendanceParticipantResponse>> AddAttendanceParticipant(AddAttendanceParticipantRequest request) =>
        SendAsync<AddAttendanceParticipantRequest, AttendanceParticipantResponse>(request);

    public Task<CmdResponse> RemoveAttendanceParticipant(RemoveAttendanceParticipantRequest request) =>
        SendVoidAsync(request);

    public Task<QueryResponse<GetAttendanceParticipantsResponse>> GetAttendanceParticipants(GetAttendanceParticipantsRequest request) =>
        SendAsync<GetAttendanceParticipantsRequest, GetAttendanceParticipantsResponse>(request);

    public Task<QueryResponse<AttendanceSessionResponse>> CreateAttendanceSession(CreateAttendanceSessionRequest request) =>
        SendAsync<CreateAttendanceSessionRequest, AttendanceSessionResponse>(request);

    public Task<QueryResponse<GetAttendanceSessionsResponse>> GetAttendanceSessions(GetAttendanceSessionsRequest request) =>
        SendAsync<GetAttendanceSessionsRequest, GetAttendanceSessionsResponse>(request);

    public Task<QueryResponse<AttendanceEventResponse>> RecordAttendanceEvent(RecordAttendanceEventRequest request) =>
        SendAsync<RecordAttendanceEventRequest, AttendanceEventResponse>(request);

    public Task<QueryResponse<AttendanceRecordResponse>> GetAttendanceRecord(GetAttendanceRecordRequest request) =>
        SendAsync<GetAttendanceRecordRequest, AttendanceRecordResponse>(request);

    public Task<QueryResponse<AttendanceReportResponse>> GetAttendanceReport(GetAttendanceReportRequest request) =>
        SendAsync<GetAttendanceReportRequest, AttendanceReportResponse>(request);

    public Task<QueryResponse<AttendanceAdjustmentResponse>> CreateAttendanceAdjustment(CreateAttendanceAdjustmentRequest request) =>
        SendAsync<CreateAttendanceAdjustmentRequest, AttendanceAdjustmentResponse>(request);

    public async Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(TargetClient)) Initialize();
        var targetClient = TargetClient ?? throw new InvalidOperationException("Target client was not initialized.");
        var (status, data) = await boltClient.InvokeAsync(targetClient, "__db_query__", queryDescriptorBytes, ct);
        if ((int)status < 200 || (int)status >= 300)
        {
            throw new InvalidOperationException(
                $"DataContext query request failed with status {(int)status} ({status}).");
        }

        return data.ToArray();
    }

    public async Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(TargetClient)) Initialize();
        var targetClient = TargetClient ?? throw new InvalidOperationException("Target client was not initialized.");
        var (status, data) = await boltClient.InvokeAsync(targetClient, "__db_changes__", saveChangesRequestBytes, ct);
        if ((int)status < 200 || (int)status >= 300)
        {
            var failure = DataContextResult.Failure(
                $"DataContext change request failed with status {(int)status} ({status}).",
                (int)status);
            return MemoryPackSerializer.Serialize(failure);
        }

        return data.ToArray();
    }

    public async IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(
        byte[] queryDescriptorBytes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(TargetClient)) Initialize();
        var targetClient = TargetClient ?? throw new InvalidOperationException("Target client was not initialized.");
        var stream = await boltClient.OpenStreamAsync(targetClient, "__db_query_stream__", ct);
        try
        {
            await stream.SendAsync((ReadOnlyMemory<byte>)queryDescriptorBytes, ct);
            await foreach (var chunk in stream.ReadAllAsync(ct))
            {
                yield return chunk.ToArray();
            }
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }
}

public static class AttendanceServiceWrapperExtensions
{
    public static void AddAttendanceWrapperServices(this IServiceCollection services)
    {
        services.AddSingleton<IAttendanceServiceWrapper, AttendanceServiceWrapper>();
    }
}
