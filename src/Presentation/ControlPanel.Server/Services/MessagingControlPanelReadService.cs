using System.Net;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Filtering;
using Messaging.Domain.Shared.Contracts.Requests.Admin;
using Messaging.Domain.Shared.Contracts.Responses;
using Messaging.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

namespace ControlPanel.Server.Services;

public sealed class MessagingControlPanelReadService(
    IMessagingServiceWrapper messaging,
    RequestMetadata metadata)
{
    public async Task<MessagingAdminUsersSummary> GetUsersSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await messaging.QueryMessagingAdminUsersAsync(
            new QueryMessagingAdminUsersRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = new MessagingAdminGridRequest { Count = 1 }
            },
            ct);

        return RequireResponse(response, "Messaging users could not be loaded.").Summary;
    }

    public async ValueTask<DataGridResult<MessagingAdminUserRow>> GetUsersAsync(
        Guid tenantId,
        DataGridRequest request)
    {
        var response = await messaging.QueryMessagingAdminUsersAsync(
            new QueryMessagingAdminUsersRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = ToAdminGridRequest(request)
            },
            request.CancellationToken);
        var data = RequireResponse(response, "Messaging users could not be loaded.");

        return new DataGridResult<MessagingAdminUserRow>
        {
            Items = data.Items,
            TotalItemCount = data.TotalItemCount
        };
    }

    public async Task<MessagingAdminUserDetailResponse?> GetUserDetailAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken ct = default)
    {
        var response = await messaging.GetMessagingAdminUserDetailAsync(
            new GetMessagingAdminUserDetailRequest
            {
                Metadata = BuildMetadata(tenantId),
                CredentialId = credentialId
            },
            ct);

        return response.HttpStatusCode == HttpStatusCode.NotFound
            ? null
            : RequireResponse(response, "Messaging user detail could not be loaded.");
    }

    public async Task<MessagingAdminThreadsSummary> GetThreadsSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await messaging.QueryMessagingAdminThreadsAsync(
            new QueryMessagingAdminThreadsRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = new MessagingAdminGridRequest { Count = 1 }
            },
            ct);

        return RequireResponse(response, "Messaging threads could not be loaded.").Summary;
    }

    public async ValueTask<DataGridResult<MessagingAdminThreadRow>> GetThreadsAsync(
        Guid tenantId,
        DataGridRequest request)
    {
        var response = await messaging.QueryMessagingAdminThreadsAsync(
            new QueryMessagingAdminThreadsRequest
            {
                Metadata = BuildMetadata(tenantId),
                Grid = ToAdminGridRequest(request)
            },
            request.CancellationToken);
        var data = RequireResponse(response, "Messaging threads could not be loaded.");

        return new DataGridResult<MessagingAdminThreadRow>
        {
            Items = data.Items,
            TotalItemCount = data.TotalItemCount
        };
    }

    public async Task<MessagingAdminThreadDetailResponse?> GetThreadDetailAsync(
        Guid tenantId,
        Guid threadId,
        CancellationToken ct = default)
    {
        var response = await messaging.GetMessagingAdminThreadDetailAsync(
            new GetMessagingAdminThreadDetailRequest
            {
                Metadata = BuildMetadata(tenantId),
                ThreadId = threadId
            },
            ct);

        return response.HttpStatusCode == HttpStatusCode.NotFound
            ? null
            : RequireResponse(response, "Messaging thread detail could not be loaded.");
    }

    public async Task<MessagingAdminOperationsResponse> GetOperationsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await messaging.GetMessagingAdminOperationsAsync(
            new GetMessagingAdminOperationsRequest
            {
                Metadata = BuildMetadata(tenantId)
            },
            ct);

        return RequireResponse(response, "Messaging operations could not be loaded.");
    }

    public async Task<MessagingAdminModerationResponse> GetModerationAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var response = await messaging.GetMessagingAdminModerationAsync(
            new GetMessagingAdminModerationRequest
            {
                Metadata = BuildMetadata(tenantId)
            },
            ct);

        return RequireResponse(response, "Messaging moderation could not be loaded.");
    }

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CredentialId = metadata.CredentialId,
        SessionId = metadata.SessionId,
        RequestId = Guid.NewGuid(),
        Name = metadata.Name,
        DeviceName = metadata.DeviceName,
        DeviceAgent = metadata.DeviceAgent,
        IpAddress = metadata.IpAddress
    };

    private static MessagingAdminGridRequest ToAdminGridRequest(DataGridRequest request) =>
        new()
        {
            StartIndex = Math.Max(request.StartIndex, 0),
            Count = request.Count.GetValueOrDefault(20),
            SearchText = request.SearchText,
            Filters = request.Filters?.Values
                .Select(filter => new MessagingAdminFilter
                {
                    Field = filter.Field,
                    Operator = ToAdminOperator(filter.Operator),
                    Value = Convert.ToString(filter.Value)
                })
                .ToList() ?? [],
            Sorts = request.SortDefinitions?
                .Select(sort => new MessagingAdminSort
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
            ? "Messaging admin read service is unavailable. Check Messaging service health and Bolt handler registration."
            : response.Message;
    }
}
