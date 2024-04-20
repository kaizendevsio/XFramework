using MediatR;
using System;
using Records.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Records.Core.DataAccess.Commands.Entity.Identity.Authorization
{
    public class UpdateAuthorizeIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateAuthorizeIdentityCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
    }
}