using System.Collections.Generic;
using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Entity.Roles
{
    public class GetRoleEntityListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<IdentityRoleEntityContract>>>
    {
        
    }
}