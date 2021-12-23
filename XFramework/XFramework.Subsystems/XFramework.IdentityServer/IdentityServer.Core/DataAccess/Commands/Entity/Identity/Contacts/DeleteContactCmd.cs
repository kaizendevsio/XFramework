using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts
{
    public class DeleteContactCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteContactCmd>>
    {
        public GenericContactType ContactType { get; set; }
        public Guid Cuid { get; set; }
        public long Cid { get; set; }
    }
}