using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts
{
    public class CreateContactCmd : CommandBaseEntity, IRequest<CmdResponseBO<CreateContactCmd>>
    {
        public GenericContactType ContactType { get; set; }
        public string Value { get; set; }
        public Guid Cuid { get; set; }
    }
}