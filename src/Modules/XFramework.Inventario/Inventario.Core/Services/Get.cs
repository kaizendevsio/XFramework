using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.DataAccess.Query;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Core.Services;
using TEntity = Domain.Shared.Contracts.Service;

public class Get(
    DbContext appDbContext,
    ILogger<Get> logger,
    ITenantService tenantService,
    IRequestHandler<Get<TEntity>, QueryResponse<TEntity>> baseHandler
)
    : IGetHandler<TEntity>, IDecorator
{
    public async Task<QueryResponse<TEntity>> Handle(Get<TEntity> request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        // Then call the base handler if needed
        return await baseHandler.Handle(request, cancellationToken);
    }
}