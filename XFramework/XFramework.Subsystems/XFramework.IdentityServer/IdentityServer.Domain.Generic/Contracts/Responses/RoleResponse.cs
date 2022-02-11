using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class RoleResponse
{
    public Guid? Guid { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime RoleExpiration { get; set; }
    public RoleEntity RoleEntityId { get; set; }
}