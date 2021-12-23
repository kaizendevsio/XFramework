using MediatR;
using System.Collections.Generic;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts;

namespace RBS.Core.DataAccess.Query.Entity.Identity
{
    public class GetAllIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetIdentityContract>>>
    {

    }
}