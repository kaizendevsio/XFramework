using System;
using IdentityServer.Domain.Generic.Enums;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential
{
    public class CreateCredentialCmd : CommandBaseEntity, IRequest<CmdResponseBO<CreateCredentialCmd>>
    {
        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
        public string PasswordString { get; set; }
        public Guid Uid { get; set; }
        public Guid? Cuid { get; set; }
        public RoleEntity RoleEntity { get; set; }
    }
}