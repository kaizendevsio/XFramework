using MediatR;
using System;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Entity.Identity.Authorization
{
    public class DeleteAuthorizeIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteAuthorizeIdentityCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }

    }
}