using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential
{
    public class UpdateCredentialCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateCredentialCmd>>
    {
        public Guid Uid { get; set; }
        public Guid Cuid { get; set; }
        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
    }
}