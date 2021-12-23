using MediatR;
using System;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity
{
    public class DeleteIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteIdentityCmd>>
    {
        public Guid Uid { get; set; }
    }
}
