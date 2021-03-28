using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using MediatR;
using System.Collections.Generic;

namespace Records.Core.DataAccess.Query.Entity.Application
{
    public class GetAppAppListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetApplicationListContract>>>
    {

    }
}