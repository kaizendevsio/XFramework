using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class IdentityRoleEntityResponse
{
    public RoleEntity Id { get; set; }
    public Guid? Guid { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Name { get; set; }
    public short? RoleLevel { get; set; }
}