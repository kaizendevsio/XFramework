namespace Messaging.Domain.Shared.Contracts.Requests.Admin;

using TUsersRequest = QueryMessagingAdminUsersRequest;
using TUsersResponse = QueryResponse<MessagingAdminUsersResponse>;
using TThreadsRequest = QueryMessagingAdminThreadsRequest;
using TThreadsResponse = QueryResponse<MessagingAdminThreadsResponse>;
using TUserDetailRequest = GetMessagingAdminUserDetailRequest;
using TUserDetailResponse = QueryResponse<MessagingAdminUserDetailResponse>;
using TThreadDetailRequest = GetMessagingAdminThreadDetailRequest;
using TThreadDetailResponse = QueryResponse<MessagingAdminThreadDetailResponse>;
using TOperationsRequest = GetMessagingAdminOperationsRequest;
using TOperationsResponse = QueryResponse<MessagingAdminOperationsResponse>;
using TModerationRequest = GetMessagingAdminModerationRequest;
using TModerationResponse = QueryResponse<MessagingAdminModerationResponse>;

[MemoryPackable]
public partial record MessagingAdminGridRequest
{
    public int StartIndex { get; set; }
    public int Count { get; set; } = 20;
    public string? SearchText { get; set; }
    public List<MessagingAdminFilter> Filters { get; set; } = [];
    public List<MessagingAdminSort> Sorts { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingAdminFilter
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "contains";
    public string? Value { get; set; }
}

[MemoryPackable]
public partial record MessagingAdminSort
{
    public string Field { get; set; } = string.Empty;
    public string Direction { get; set; } = "none";
}

[MemoryPackable]
public partial record QueryMessagingAdminUsersRequest : RequestBase,
    IQuery<TUsersResponse>,
    IBoltRequest<TUsersRequest, TUsersResponse>
{
    public MessagingAdminGridRequest Grid { get; set; } = new();
}

[MemoryPackable]
public partial record QueryMessagingAdminThreadsRequest : RequestBase,
    IQuery<TThreadsResponse>,
    IBoltRequest<TThreadsRequest, TThreadsResponse>
{
    public MessagingAdminGridRequest Grid { get; set; } = new();
}

[MemoryPackable]
public partial record GetMessagingAdminUserDetailRequest : RequestBase,
    IQuery<TUserDetailResponse>,
    IBoltRequest<TUserDetailRequest, TUserDetailResponse>
{
    public Guid CredentialId { get; set; }
}

[MemoryPackable]
public partial record GetMessagingAdminThreadDetailRequest : RequestBase,
    IQuery<TThreadDetailResponse>,
    IBoltRequest<TThreadDetailRequest, TThreadDetailResponse>
{
    public Guid ThreadId { get; set; }
}

[MemoryPackable]
public partial record GetMessagingAdminOperationsRequest : RequestBase,
    IQuery<TOperationsResponse>,
    IBoltRequest<TOperationsRequest, TOperationsResponse>;

[MemoryPackable]
public partial record GetMessagingAdminModerationRequest : RequestBase,
    IQuery<TModerationResponse>,
    IBoltRequest<TModerationRequest, TModerationResponse>;
