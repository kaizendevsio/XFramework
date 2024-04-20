using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageThreadMemberRoleRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? RoleGuid { get; set; }
}