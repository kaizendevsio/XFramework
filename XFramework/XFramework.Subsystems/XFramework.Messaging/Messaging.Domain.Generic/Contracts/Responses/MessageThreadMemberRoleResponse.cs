using Messaging.Domain.DataTransferObjects;

namespace Messaging.Domain.Generic.Contracts.Responses;

public class MessageThreadMemberRoleResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Guid { get; set; }

    public MessageThreadMemberResponse? ThreadMember { get; set; }
    public IdentityRole? Role { get; set; }
}