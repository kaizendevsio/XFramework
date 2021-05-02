using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.Identity
{
    public class DeleteIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteIdentityCmd>>
    {
        public Guid Uid { get; set; }
    }
}