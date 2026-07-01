using System.Net;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Filtering;
using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

namespace ControlPanel.Server.Services;

public sealed class CommunicationsControlPanelReadService(
    ICommunicationsServiceWrapper communications,
    RequestMetadata metadata)
{
    public async Task<CommunicationsAdminUsersSummary> GetUsersSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await communications.QueryCommunicationsAdminUsersAsync(
            new QueryCommunicationsAdminUsersRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = new CommunicationsAdminGridRequest { Count = 1 }
            },
            ct);

        return RequireResponse(response, "Communications users could not be loaded.").Summary;
    }

    public async ValueTask<DataGridResult<CommunicationsAdminUserRow>> GetUsersAsync(
        Guid tenantId,
        DataGridRequest request)
    {
        var response = await communications.QueryCommunicationsAdminUsersAsync(
            new QueryCommunicationsAdminUsersRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = ToAdminGridRequest(request)
            },
            request.CancellationToken);
        var data = RequireResponse(response, "Communications users could not be loaded.");

        return new DataGridResult<CommunicationsAdminUserRow>
        {
            Items = data.Items,
            TotalItemCount = data.TotalItemCount
        };
    }

    public async Task<CommunicationsAdminUserDetailResponse?> GetUserDetailAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken ct = default)
    {
        var response = await communications.GetCommunicationsAdminUserDetailAsync(
            new GetCommunicationsAdminUserDetailRequest
            {
                Metadata = BuildMetadata(tenantId),
                CredentialId = credentialId
            },
            ct);

        return response.HttpStatusCode == HttpStatusCode.NotFound
            ? null
            : RequireResponse(response, "Communications user detail could not be loaded.");
    }

    public async Task<CommunicationsAdminThreadsSummary> GetThreadsSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await communications.QueryCommunicationsAdminThreadsAsync(
            new QueryCommunicationsAdminThreadsRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = new CommunicationsAdminGridRequest { Count = 1 }
            },
            ct);

        return RequireResponse(response, "Communications threads could not be loaded.").Summary;
    }

    public async ValueTask<DataGridResult<CommunicationsAdminThreadRow>> GetThreadsAsync(
        Guid tenantId,
        DataGridRequest request)
    {
        var response = await communications.QueryCommunicationsAdminThreadsAsync(
            new QueryCommunicationsAdminThreadsRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = ToAdminGridRequest(request)
            },
            request.CancellationToken);
        var data = RequireResponse(response, "Communications threads could not be loaded.");

        return new DataGridResult<CommunicationsAdminThreadRow>
        {
            Items = data.Items,
            TotalItemCount = data.TotalItemCount
        };
    }

    public async Task<CommunicationsAdminThreadDetailResponse?> GetThreadDetailAsync(
        Guid tenantId,
        Guid threadId,
        CancellationToken ct = default)
    {
        var response = await communications.GetCommunicationsAdminThreadDetailAsync(
            new GetCommunicationsAdminThreadDetailRequest
            {
                Metadata = BuildMetadata(tenantId),
                ThreadId = threadId
            },
            ct);

        return response.HttpStatusCode == HttpStatusCode.NotFound
            ? null
            : RequireResponse(response, "Communications thread detail could not be loaded.");
    }

    public async Task<CommunicationsAdminOperationsResponse> GetOperationsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await communications.GetCommunicationsAdminOperationsAsync(
            new GetCommunicationsAdminOperationsRequest
            {
                Metadata = BuildMetadata(tenantId)
            },
            ct);

        return RequireResponse(response, "Communications operations could not be loaded.");
    }

    public async Task<CommunicationsAdminModerationResponse> GetModerationAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await communications.GetCommunicationsAdminModerationAsync(
            new GetCommunicationsAdminModerationRequest
            {
                Metadata = BuildMetadata(tenantId)
            },
            ct);

        return RequireResponse(response, "Communications moderation could not be loaded.");
    }

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CredentialId = metadata.CredentialId,
        SessionId = metadata.SessionId,
        RequestId = Guid.NewGuid(),
        Name = "ControlPanel",
        DeviceName = metadata.DeviceName,
        DeviceAgent = metadata.DeviceAgent,
        IpAddress = metadata.IpAddress
    };

    private static CommunicationsAdminGridRequest ToAdminGridRequest(DataGridRequest request) =>
        new()
        {
            StartIndex = Math.Max(request.StartIndex, 0),
            Count = request.Count.GetValueOrDefault(20),
            SearchText = request.SearchText,
            Filters = request.Filters?.Values
                .Select(filter => new CommunicationsAdminFilter
                {
                    Field = filter.Field,
                    Operator = ToAdminOperator(filter.Operator),
                    Value = Convert.ToString(filter.Value)
                })
                .ToList() ?? [],
            Sorts = request.SortDefinitions?
                .Select(sort => new CommunicationsAdminSort
                {
                    Field = sort.ColumnId,
                    Direction = sort.Direction switch
                    {
                        SortDirection.Ascending => "ascending",
                        SortDirection.Descending => "descending",
                        _ => "none"
                    }
                })
                .ToList() ?? []
        };

    private static string ToAdminOperator(FilterOperator filterOperator) =>
        filterOperator switch
        {
            FilterOperator.Equals => "equals",
            FilterOperator.NotEquals => "notEquals",
            FilterOperator.IsEmpty => "isEmpty",
            FilterOperator.IsNotEmpty => "isNotEmpty",
            FilterOperator.NotContains => "notContains",
            FilterOperator.StartsWith => "startsWith",
            FilterOperator.EndsWith => "endsWith",
            FilterOperator.IsTrue => "isTrue",
            FilterOperator.IsFalse => "isFalse",
            _ => "contains"
        };

    private static T RequireResponse<T>(QueryResponse<T> response, string fallback)
    {
        if (response is { IsSuccess: true, Response: not null })
        {
            return response.Response;
        }

        throw new InvalidOperationException(NormalizeFailureMessage(response, fallback));
    }

    private static string NormalizeFailureMessage<T>(QueryResponse<T> response, string fallback)
    {
        if (string.IsNullOrWhiteSpace(response.Message))
        {
            return fallback;
        }

        return string.Equals(response.Message, "NotFound", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(response.Message, "NotImplemented", StringComparison.OrdinalIgnoreCase)
            ? "Communications admin read service is unavailable. Check Communications service health and Bolt handler registration."
            : response.Message;
    }
}
