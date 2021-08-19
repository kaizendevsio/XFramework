using System.Collections.Generic;
using MediatR;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Entity.Game
{
    public class LatestGameQuery : QueryBaseEntity, IRequest<QueryResponseBO<LatestGameContract>>
    {
        
    }
}