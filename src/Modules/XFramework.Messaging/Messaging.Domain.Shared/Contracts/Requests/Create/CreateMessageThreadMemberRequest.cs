using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageThreadMemberRequest : RequestBase
{
    public short Status { get; set; }
    public string? Emoji { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public Guid? MessageThreadGuid { get; set; }
    public Guid? IdentityCredentialGuid { get; set; }
    public Guid? GroupGuid { get; set; }
}