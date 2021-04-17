using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.User.Credential
{
    public class CreateCredentialCmd : CommandBaseEntity, IRequest<CmdResponseBO<CreateCredentialCmd>>
    {
        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string PasswordString { get; set; }
        public Guid Uid { get; set; }
    }
}