using MediatR;
using System.Collections.Generic;
using Coins.Domain.BusinessObjects;
using Coins.Domain.Contracts;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.DataAccess.Query.Entity.Application
{
    public class GetAppAppListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetApplicationListContract>>>
    {

    }
}