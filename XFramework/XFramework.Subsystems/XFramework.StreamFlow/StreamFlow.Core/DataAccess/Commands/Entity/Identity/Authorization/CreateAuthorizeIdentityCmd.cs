using MediatR;
using System;
using StreamFlow.Domain.BusinessObjects;

namespace StreamFlow.Core.DataAccess.Commands.Entity.Identity.Authorization
{
    public class CreateAuthorizeIdentityCmd : CommandBaseEntity, IRequest<CmdResponseBO<CreateAuthorizeIdentityCmd>>
    {
        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string Token { get; set; }
        public string PasswordString { get; set; }
        public Guid Uid { get; set; }
    }
}