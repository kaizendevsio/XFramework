using MediatR;
using System.Collections.Generic;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Contracts;

namespace StreamFlow.Core.DataAccess.Query.Entity.Identity
{
    public class GetAllIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetIdentityContract>>>
    {

    }
}