using System;
using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Responses
{
    public class IdentityRoleContract
    {
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime RoleExpiration { get; set; }
        public RoleEntity RoleEntityId { get; set; }
    }
}