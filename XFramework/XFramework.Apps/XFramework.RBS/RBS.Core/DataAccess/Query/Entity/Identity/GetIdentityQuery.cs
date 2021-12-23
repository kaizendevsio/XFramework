using MediatR;
using System;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts;

namespace RBS.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<GetIdentityContract>>
    {
        public Guid Uid { get; set; }
    }
}