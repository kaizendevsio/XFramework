using MediatR;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Community.Domain.Shared.Contracts.Requests;

public record UpdateCommunityIdentityRequest : RequestBase, IRequest<CmdResponse>
{
    public Guid CredentialId { get; set; }
    public Guid Id { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
}