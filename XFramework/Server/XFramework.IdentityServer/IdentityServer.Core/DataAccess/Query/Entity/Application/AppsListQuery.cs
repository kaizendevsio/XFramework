using System.Collections.Generic;
using IdentityServer.Domain.BO;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DTO;
using MediatR;

namespace IdentityServer.Core.DataAccess.Query.Entity.Application
{
    public class AppsListQuery : IRequest<QueryResponseBO<List<GetApplicationListContract>>>
    {
        
    }
}