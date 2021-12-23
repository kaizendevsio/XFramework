using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential
{
    public class DeleteCredentialCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteCredentialCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
        
    }
}