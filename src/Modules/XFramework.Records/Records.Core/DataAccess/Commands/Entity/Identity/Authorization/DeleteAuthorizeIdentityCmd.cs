using MediatR;
using System;
using Records.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Records.Core.DataAccess.Commands.Entity.Identity.Authorization
{
    public class DeleteAuthorizeIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteAuthorizeIdentityCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }

    }
}