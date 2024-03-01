using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests;

public record UpdateCommunityIdentityRequest : RequestBase, IRequest<CmdResponse>
{
    public Guid CredentialId { get; set; }
    public Guid Id { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
}