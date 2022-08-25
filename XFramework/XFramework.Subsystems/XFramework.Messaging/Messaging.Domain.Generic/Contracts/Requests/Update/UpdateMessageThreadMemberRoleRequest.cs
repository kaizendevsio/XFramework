using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public class UpdateMessageThreadMemberRoleRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? RoleGuid { get; set; }
}