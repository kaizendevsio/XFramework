namespace Communications.Domain.Shared.Contracts.Requests.Admin;

using TUsersRequest = QueryCommunicationsAdminUsersRequest;
using TUsersResponse = QueryResponse<CommunicationsAdminUsersResponse>;
using TThreadsRequest = QueryCommunicationsAdminThreadsRequest;
using TThreadsResponse = QueryResponse<CommunicationsAdminThreadsResponse>;
using TUserDetailRequest = GetCommunicationsAdminUserDetailRequest;
using TUserDetailResponse = QueryResponse<CommunicationsAdminUserDetailResponse>;
using TThreadDetailRequest = GetCommunicationsAdminThreadDetailRequest;
using TThreadDetailResponse = QueryResponse<CommunicationsAdminThreadDetailResponse>;
using TOperationsRequest = GetCommunicationsAdminOperationsRequest;
using TOperationsResponse = QueryResponse<CommunicationsAdminOperationsResponse>;
using TModerationRequest = GetCommunicationsAdminModerationRequest;
using TModerationResponse = QueryResponse<CommunicationsAdminModerationResponse>;

[MemoryPackable]
public partial record CommunicationsAdminGridRequest
{
    public int StartIndex { get; set; }
    public int Count { get; set; } = 20;
    public string? SearchText { get; set; }
    public List<CommunicationsAdminFilter> Filters { get; set; } = [];
    public List<CommunicationsAdminSort> Sorts { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsAdminFilter
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "contains";
    public string? Value { get; set; }
}

[MemoryPackable]
public partial record CommunicationsAdminSort
{
    public string Field { get; set; } = string.Empty;
    public string Direction { get; set; } = "none";
}

[MemoryPackable]
public partial record QueryCommunicationsAdminUsersRequest : RequestBase,
    IQuery<TUsersResponse>,
    IBoltRequest<TUsersRequest, TUsersResponse>
{
    public CommunicationsAdminGridRequest Grid { get; set; } = new();
}

[MemoryPackable]
public partial record QueryCommunicationsAdminThreadsRequest : RequestBase,
    IQuery<TThreadsResponse>,
    IBoltRequest<TThreadsRequest, TThreadsResponse>
{
    public CommunicationsAdminGridRequest Grid { get; set; } = new();
}

[MemoryPackable]
public partial record GetCommunicationsAdminUserDetailRequest : RequestBase,
    IQuery<TUserDetailResponse>,
    IBoltRequest<TUserDetailRequest, TUserDetailResponse>
{
    public Guid CredentialId { get; set; }
}

[MemoryPackable]
public partial record GetCommunicationsAdminThreadDetailRequest : RequestBase,
    IQuery<TThreadDetailResponse>,
    IBoltRequest<TThreadDetailRequest, TThreadDetailResponse>
{
    public Guid ThreadId { get; set; }
}

[MemoryPackable]
public partial record GetCommunicationsAdminOperationsRequest : RequestBase,
    IQuery<TOperationsResponse>,
    IBoltRequest<TOperationsRequest, TOperationsResponse>;

[MemoryPackable]
public partial record GetCommunicationsAdminModerationRequest : RequestBase,
    IQuery<TModerationResponse>,
    IBoltRequest<TModerationRequest, TModerationResponse>;
