using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageThreadMemberRoleRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? RoleGuid { get; set; }
}