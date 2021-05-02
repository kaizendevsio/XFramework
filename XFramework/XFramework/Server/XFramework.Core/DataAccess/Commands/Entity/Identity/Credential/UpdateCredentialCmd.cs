using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.Identity.Credential
{
    public class UpdateCredentialCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateCredentialCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
    }
}