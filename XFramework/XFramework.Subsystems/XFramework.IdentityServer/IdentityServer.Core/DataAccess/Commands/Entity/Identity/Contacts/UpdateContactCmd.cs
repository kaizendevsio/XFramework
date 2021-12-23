using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts
{
    public class UpdateContactCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateContactCmd>>
    {
        public GenericContactType ContactType { get; set; }
        public string Value { get; set; }
        public string Cuid { get; set; }
        public long Cid { get; set; }
    }
}