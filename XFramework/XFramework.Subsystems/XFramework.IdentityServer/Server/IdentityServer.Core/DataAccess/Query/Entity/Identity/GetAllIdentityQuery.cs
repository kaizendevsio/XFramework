using System.Collections.Generic;
using IdentityServer.Domain.Contracts;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity
{
    public class GetAllIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetIdentityContract>>>
    {
        
    }
}