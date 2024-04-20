using MediatR;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Community.Domain.Shared.Contracts.Requests;

public record CreateCommunityIdentityRequest : RequestBase, IRequest<CmdResponse>
{
    public Guid CredentialId { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
    public string? Tagline { get; set; }
    public string? Alias { get; set; }
    public string? HandleName { get; set; }
}