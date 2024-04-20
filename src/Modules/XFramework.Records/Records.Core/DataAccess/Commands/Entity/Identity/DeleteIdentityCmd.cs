using MediatR;
using System;
using Records.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Records.Core.DataAccess.Commands.Entity.Identity
{
    public class DeleteIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteIdentityCmd>>
    {
        public Guid Uid { get; set; }
    }
}
