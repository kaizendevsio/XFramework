using System;
using IdentityServer.Domain.Generic.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

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