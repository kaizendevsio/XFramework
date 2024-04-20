using MediatR;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;

public record GetCommunityConnectionListRequest : RequestBase, IRequest<QueryResponse<List<CommunityConnection>>>
{
    public Guid ConnectionTypeId { get; set; }    
    public Guid CommunityIdentityId { get; set; }
    public int Limit { get; set; }
}