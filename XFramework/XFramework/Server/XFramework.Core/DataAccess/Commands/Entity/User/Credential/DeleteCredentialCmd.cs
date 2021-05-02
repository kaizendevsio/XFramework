using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.User.Credential
{
    public class DeleteCredentialCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteCredentialCmd>>
    {
        public Guid Uid { get; set; }
        public string Username { get; set; }   
    }
}