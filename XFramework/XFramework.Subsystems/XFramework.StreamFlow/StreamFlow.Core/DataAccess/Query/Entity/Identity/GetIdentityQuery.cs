using MediatR;
using System;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Contracts;

namespace StreamFlow.Core.DataAccess.Query.Entity.Identity
{
    public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<GetIdentityContract>>
    {
        public Guid Uid { get; set; }
    }
}