using System.Collections.Generic;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using MediatR;

namespace IdentityServer.Core.DataAccess.Query.Entity.Application
{
    public class GetAppAppListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetApplicationListContract>>>
    {
        
    }
}