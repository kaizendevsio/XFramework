using MediatR;
using System;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Entity.Identity
{
    public class DeleteIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteIdentityCmd>>
    {
        public Guid Uid { get; set; }
    }
}
