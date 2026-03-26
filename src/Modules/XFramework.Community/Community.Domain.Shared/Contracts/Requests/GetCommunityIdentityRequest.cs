using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

using TRequest = GetCommunityIdentityRequest;
using TResponse = QueryResponse<GetCommunityIdentityResponse>;

[MemoryPackable]
public partial record GetCommunityIdentityRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid Id { get; set; }
}
