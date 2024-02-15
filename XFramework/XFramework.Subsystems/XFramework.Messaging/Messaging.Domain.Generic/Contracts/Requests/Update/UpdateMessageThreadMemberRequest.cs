using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public record UpdateMessageThreadMemberRequest : RequestBase
{
    public short Status { get; set; }
    public string? Emoji { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public Guid? MessageThreadGuid { get; set; }
    public Guid? IdentityCredentialGuid { get; set; }
    public Guid? GroupGuid { get; set; }
}