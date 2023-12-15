using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests;

public record CreateCommunityIdentityRequest : RequestBase, IRequest<CmdResponse>
{
    public Guid CredentialId { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
    public string? Tagline { get; set; }
    public string? Alias { get; set; }
    public string? HandleName { get; set; }
}