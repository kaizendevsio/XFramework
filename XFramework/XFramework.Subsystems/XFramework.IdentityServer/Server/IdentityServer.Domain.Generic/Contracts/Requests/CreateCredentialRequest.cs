using System;
using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class CreateCredentialRequest : RequestBase
    {
        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
        public string PasswordString { get; set; }
        public RoleEntity RoleEntity { get; set; }
        public Guid Uid { get; set; }
        public Guid? Cuid { get; set; }
    }
}