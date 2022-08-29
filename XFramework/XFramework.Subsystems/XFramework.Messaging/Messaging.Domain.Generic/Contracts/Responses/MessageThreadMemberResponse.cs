
namespace Messaging.Domain.Generic.Contracts.Responses;

public class MessageThreadMemberResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public short Status { get; set; }
    public string? Emoji { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? Guid { get; set; }

    public MessageThreadResponse? Thread { get; set; }
    public MessageThreadMemberGroupResponse? Group { get; set; }
    public IdentityCredential? Credential { get; set; }

}