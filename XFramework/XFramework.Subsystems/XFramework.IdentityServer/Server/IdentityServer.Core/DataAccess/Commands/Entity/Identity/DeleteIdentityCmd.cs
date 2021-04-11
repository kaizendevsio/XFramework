using MediatR;
using System;
using IdentityServer.Domain.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity
{
    public class DeleteIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteIdentityCmd>>
    {
        public Guid Uid { get; set; }
    }
}
