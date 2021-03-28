using System.Collections.Generic;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using MediatR;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity
{
    public class GetAllIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetIdentityContract>>>
    {
        
    }
}