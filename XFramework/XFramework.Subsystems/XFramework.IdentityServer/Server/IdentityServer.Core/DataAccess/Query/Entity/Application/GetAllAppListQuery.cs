using System.Collections.Generic;
using IdentityServer.Domain.Contracts;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Entity.Application
{
    public class GetAppAppListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetApplicationListContract>>>
    {
        
    }
}