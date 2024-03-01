using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts;
using XFramework.Domain.Generic.Contracts.Requests;

public record GetCommunityConnectionListRequest : RequestBase, IRequest<QueryResponse<List<CommunityConnection>>>
{
    public Guid ConnectionTypeId { get; set; }    
    public Guid CommunityIdentityId { get; set; }
    public int Limit { get; set; }
}