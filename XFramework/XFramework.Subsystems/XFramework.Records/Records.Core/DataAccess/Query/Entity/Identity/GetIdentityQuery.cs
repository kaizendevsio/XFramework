using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using MediatR;
using System;

namespace Records.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<GetIdentityContract>>
    {
        public Guid Uid { get; set; }
    }
}