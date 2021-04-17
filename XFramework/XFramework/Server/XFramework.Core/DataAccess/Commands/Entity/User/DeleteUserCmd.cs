using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.User
{
    public class DeleteUserCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteUserCmd>>
    {
        public Guid Uid { get; set; }
    }
}