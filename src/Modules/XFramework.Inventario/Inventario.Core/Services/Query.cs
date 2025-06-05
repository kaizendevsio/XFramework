using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Query;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Core.Services;
using TEntity = Domain.Shared.Contracts.Service;

public class Query(
    DbContext appDbContext,
    ILogger<Query> logger,
    ITenantService tenantService,
    IRequestHandler<GetList<TEntity>, QueryResponse<PaginatedResult<TEntity>>> baseHandler
)
    : IGetListHandler<TEntity>, IDecorator
{
    public async Task<QueryResponse<PaginatedResult<TEntity>>> Handle(GetList<TEntity> request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        // Then call the base handler if needed
        return await baseHandler.Handle(request, cancellationToken);
    }
}