using MediatR;
using System;
using StreamFlow.Domain.BusinessObjects;

namespace StreamFlow.Core.DataAccess.Commands.Entity.Identity.Authorization
{
    public class UpdateAuthorizeIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateAuthorizeIdentityCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }
    }
}