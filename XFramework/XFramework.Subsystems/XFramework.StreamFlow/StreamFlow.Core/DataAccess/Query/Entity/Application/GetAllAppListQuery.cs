using MediatR;
using System.Collections.Generic;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Contracts;

namespace StreamFlow.Core.DataAccess.Query.Entity.Application
{
    public class GetAppAppListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetApplicationListContract>>>
    {

    }
}