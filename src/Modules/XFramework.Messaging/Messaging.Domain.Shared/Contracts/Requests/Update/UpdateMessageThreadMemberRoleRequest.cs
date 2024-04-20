using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageThreadMemberRoleRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? RoleGuid { get; set; }
}