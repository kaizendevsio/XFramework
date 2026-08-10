using System.Net;
using Attendance.Domain.Shared.Contracts.Requests;
using Attendance.Domain.Shared.Contracts.Responses;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Attendance.Integration.Drivers;

public interface IAttendanceServiceWrapper : IServiceWrapper, IDataContextServiceWrapper
{
    Task<QueryResponse<AttendanceContextResponse>> CreateAttendanceContext(
        CreateAttendanceContextRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceContextResponse>> UpdateAttendanceContext(
        UpdateAttendanceContextRequest request, CancellationToken ct = default);
    Task<QueryResponse<GetAttendanceContextsResponse>> GetAttendanceContexts(
        GetAttendanceContextsRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceParticipantResponse>> AddAttendanceParticipant(
        AddAttendanceParticipantRequest request, CancellationToken ct = default);
    Task<CmdResponse> RemoveAttendanceParticipant(
        RemoveAttendanceParticipantRequest request, CancellationToken ct = default);
    Task<QueryResponse<GetAttendanceParticipantsResponse>> GetAttendanceParticipants(
        GetAttendanceParticipantsRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceSessionResponse>> CreateAttendanceSession(
        CreateAttendanceSessionRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceSessionResponse>> TransitionAttendanceSession(
        TransitionAttendanceSessionRequest request, CancellationToken ct = default);
    Task<QueryResponse<GetAttendanceSessionsResponse>> GetAttendanceSessions(
        GetAttendanceSessionsRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceEventResponse>> RecordAttendanceEvent(
        RecordAttendanceEventRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceRecordResponse>> GetAttendanceRecord(
        GetAttendanceRecordRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceReportResponse>> GetAttendanceReport(
        GetAttendanceReportRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceAdjustmentResponse>> CreateAttendanceAdjustment(
        CreateAttendanceAdjustmentRequest request, CancellationToken ct = default);
    Task<QueryResponse<GetAttendanceContextOverviewResponse>> GetAttendanceContextOverview(
        GetAttendanceContextOverviewRequest request, CancellationToken ct = default);
    Task<QueryResponse<GetAttendanceSessionReadListResponse>> GetAttendanceSessionReadList(
        GetAttendanceSessionReadListRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceSessionDetailReadResponse>> GetAttendanceSessionDetailRead(
        GetAttendanceSessionDetailReadRequest request, CancellationToken ct = default);
    Task<QueryResponse<GetAttendanceParticipantReadListResponse>> GetAttendanceParticipantReadList(
        GetAttendanceParticipantReadListRequest request, CancellationToken ct = default);
    Task<QueryResponse<AttendanceCredentialHistoryResponse>> GetAttendanceCredentialHistory(
        GetAttendanceCredentialHistoryRequest request, CancellationToken ct = default);
}

public sealed record AttendanceServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration,
    BoltClient boltClient,
    IServiceTokenProvider serviceTokenProvider,
    IActorAccessTokenProvider actorAccessTokenProvider
) : DriverBase(messageBusDriver, configuration), IAttendanceServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = XFrameworkServiceNames.Attendance.ToSha256();
    }

    public Task<QueryResponse<AttendanceContextResponse>> CreateAttendanceContext(
        CreateAttendanceContextRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<CreateAttendanceContextRequest, AttendanceContextResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<AttendanceContextResponse>> UpdateAttendanceContext(
        UpdateAttendanceContextRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<UpdateAttendanceContextRequest, AttendanceContextResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<GetAttendanceContextsResponse>> GetAttendanceContexts(
        GetAttendanceContextsRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceContextsRequest, GetAttendanceContextsResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceParticipantResponse>> AddAttendanceParticipant(
        AddAttendanceParticipantRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<AddAttendanceParticipantRequest, AttendanceParticipantResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<CmdResponse> RemoveAttendanceParticipant(
        RemoveAttendanceParticipantRequest request, CancellationToken ct = default) =>
        SendBusinessVoidAsync(request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<GetAttendanceParticipantsResponse>> GetAttendanceParticipants(
        GetAttendanceParticipantsRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceParticipantsRequest, GetAttendanceParticipantsResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceSessionResponse>> CreateAttendanceSession(
        CreateAttendanceSessionRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<CreateAttendanceSessionRequest, AttendanceSessionResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<AttendanceSessionResponse>> TransitionAttendanceSession(
        TransitionAttendanceSessionRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<TransitionAttendanceSessionRequest, AttendanceSessionResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<GetAttendanceSessionsResponse>> GetAttendanceSessions(
        GetAttendanceSessionsRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceSessionsRequest, GetAttendanceSessionsResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceEventResponse>> RecordAttendanceEvent(
        RecordAttendanceEventRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<RecordAttendanceEventRequest, AttendanceEventResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<AttendanceRecordResponse>> GetAttendanceRecord(
        GetAttendanceRecordRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceRecordRequest, AttendanceRecordResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceReportResponse>> GetAttendanceReport(
        GetAttendanceReportRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceReportRequest, AttendanceReportResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceAdjustmentResponse>> CreateAttendanceAdjustment(
        CreateAttendanceAdjustmentRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<CreateAttendanceAdjustmentRequest, AttendanceAdjustmentResponse>(
            request, XFrameworkServiceScopes.AttendanceWrite, ct);

    public Task<QueryResponse<GetAttendanceContextOverviewResponse>> GetAttendanceContextOverview(
        GetAttendanceContextOverviewRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceContextOverviewRequest, GetAttendanceContextOverviewResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<GetAttendanceSessionReadListResponse>> GetAttendanceSessionReadList(
        GetAttendanceSessionReadListRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceSessionReadListRequest, GetAttendanceSessionReadListResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceSessionDetailReadResponse>> GetAttendanceSessionDetailRead(
        GetAttendanceSessionDetailReadRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceSessionDetailReadRequest, AttendanceSessionDetailReadResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<GetAttendanceParticipantReadListResponse>> GetAttendanceParticipantReadList(
        GetAttendanceParticipantReadListRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceParticipantReadListRequest, GetAttendanceParticipantReadListResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    public Task<QueryResponse<AttendanceCredentialHistoryResponse>> GetAttendanceCredentialHistory(
        GetAttendanceCredentialHistoryRequest request, CancellationToken ct = default) =>
        SendBusinessAsync<GetAttendanceCredentialHistoryRequest, AttendanceCredentialHistoryResponse>(
            request, XFrameworkServiceScopes.AttendanceRead, ct);

    private async Task<QueryResponse<TResponse>> SendBusinessAsync<TRequest, TResponse>(
        TRequest request,
        string requiredScope,
        CancellationToken ct)
        where TRequest : class, IHasRequestServer
    {
        var targetClient = GetTargetClient();
        PrepareRequest(request);
        var payload = await BoltInvocationEnvelopeFactory.CreateAsync(
            request,
            targetClient,
            [requiredScope],
            serviceTokenProvider,
            actorAccessTokenProvider,
            ct);
        var (status, responsePayload) = await boltClient.InvokeAsync(
            targetClient, typeof(TRequest).Name, payload, ct);
        return DeserializeQueryResponse<TResponse>(status, responsePayload);
    }

    private async Task<CmdResponse> SendBusinessVoidAsync<TRequest>(
        TRequest request,
        string requiredScope,
        CancellationToken ct)
        where TRequest : class, IHasRequestServer
    {
        var targetClient = GetTargetClient();
        PrepareRequest(request);
        var payload = await BoltInvocationEnvelopeFactory.CreateAsync(
            request,
            targetClient,
            [requiredScope],
            serviceTokenProvider,
            actorAccessTokenProvider,
            ct);
        var (status, responsePayload) = await boltClient.InvokeAsync(
            targetClient, typeof(TRequest).Name, payload, ct);
        return DeserializeCmdResponse(status, responsePayload);
    }

    private string GetTargetClient()
    {
        if (string.IsNullOrWhiteSpace(TargetClient))
            Initialize();

        return TargetClient ?? throw new InvalidOperationException("Target client was not initialized.");
    }

    private static void PrepareRequest<TRequest>(TRequest request)
        where TRequest : IHasRequestServer
    {
        request.Metadata ??= new RequestMetadata();
        request.Metadata.OperationName ??= typeof(TRequest).Name;
        request.Metadata.RequestId ??= Guid.NewGuid();
    }

    private static QueryResponse<TResponse> DeserializeQueryResponse<TResponse>(
        HttpStatusCode status,
        ReadOnlyMemory<byte> responsePayload)
    {
        if (responsePayload.IsEmpty)
            return new QueryResponse<TResponse> { HttpStatusCode = status, Message = status.ToString() };

        try
        {
            var wrapped = MemoryPackSerializer.Deserialize<QueryResponse<TResponse>>(responsePayload.Span);
            if (wrapped is not null)
                return wrapped;
        }
        catch (MemoryPackSerializationException)
        {
            // Legacy handlers may serialize only the response body.
        }

        return new QueryResponse<TResponse>
        {
            HttpStatusCode = status,
            Message = status.ToString(),
            Response = MemoryPackSerializer.Deserialize<TResponse>(responsePayload.Span)
        };
    }

    private static CmdResponse DeserializeCmdResponse(
        HttpStatusCode status,
        ReadOnlyMemory<byte> responsePayload)
    {
        if (!responsePayload.IsEmpty)
        {
            try
            {
                var wrapped = MemoryPackSerializer.Deserialize<CmdResponse>(responsePayload.Span);
                if (wrapped is not null)
                    return wrapped;
            }
            catch (MemoryPackSerializationException)
            {
                // Legacy handlers may return no command envelope payload.
            }
        }

        return new CmdResponse { HttpStatusCode = status, Message = status.ToString() };
    }

    public async Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(TargetClient)) Initialize();
        var targetClient = TargetClient ?? throw new InvalidOperationException("Target client was not initialized.");
        var descriptor = MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes)
            ?? throw new InvalidOperationException("Query descriptor could not be deserialized.");
        descriptor.Metadata ??= new RequestMetadata();
        descriptor.Metadata.RequestId ??= Guid.NewGuid();
        IReadOnlyCollection<string> scopes = descriptor.IgnoreQueryFilters
            ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
            : [XFrameworkServiceScopes.DataContextQuery];
        var payload = await BoltInvocationEnvelopeFactory.CreateAsync(
            descriptor, targetClient, scopes, serviceTokenProvider, actorAccessTokenProvider, ct);
        var (status, data) = await boltClient.InvokeAsync(targetClient, "__db_query__", payload, ct);
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
        var request = MemoryPackSerializer.Deserialize<SaveChangesRequest>((ReadOnlySpan<byte>)saveChangesRequestBytes)
            ?? throw new InvalidOperationException("SaveChanges request could not be deserialized.");
        request.Metadata ??= new RequestMetadata();
        request.Metadata.RequestId ??= Guid.NewGuid();
        var payload = await BoltInvocationEnvelopeFactory.CreateAsync(
            request,
            targetClient,
            [XFrameworkServiceScopes.DataContextMutate],
            serviceTokenProvider,
            actorAccessTokenProvider,
            ct);
        var (status, data) = await boltClient.InvokeAsync(targetClient, "__db_changes__", payload, ct);
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
        var descriptor = MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes)
            ?? throw new InvalidOperationException("Query descriptor could not be deserialized.");
        descriptor.Metadata ??= new RequestMetadata();
        descriptor.Metadata.RequestId ??= Guid.NewGuid();
        IReadOnlyCollection<string> scopes = descriptor.IgnoreQueryFilters
            ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
            : [XFrameworkServiceScopes.DataContextQuery];
        var payload = await BoltInvocationEnvelopeFactory.CreateAsync(
            descriptor, targetClient, scopes, serviceTokenProvider, actorAccessTokenProvider, ct);
        var stream = await boltClient.OpenStreamAsync(targetClient, "__db_query_stream__", ct);
        try
        {
            await stream.SendAsync((ReadOnlyMemory<byte>)payload, ct);
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
        services.AddScoped<IAttendanceServiceWrapper, AttendanceServiceWrapper>();
    }
}
