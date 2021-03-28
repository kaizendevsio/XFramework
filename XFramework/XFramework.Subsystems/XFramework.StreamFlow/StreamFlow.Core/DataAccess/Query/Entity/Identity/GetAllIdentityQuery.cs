using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using MediatR;
using System.Collections.Generic;

namespace StreamFlow.Core.DataAccess.Query.Entity.Identity
{
    public class GetAllIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetIdentityContract>>>
    {

    }
}