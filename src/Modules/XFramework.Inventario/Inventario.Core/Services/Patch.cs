using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.DataAccess.Query;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Core.Services;
using TEntity = Domain.Shared.Contracts.Service;

public class Patch(
    DbContext appDbContext,
    ILogger<Patch> logger,
    ITenantService tenantService,
    IRequestHandler<Patch<TEntity>, CmdResponse<TEntity>> baseHandler
)
    : IPatchHandler<TEntity>, IDecorator
{
    public async Task<CmdResponse<TEntity>> Handle(Patch<TEntity> request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        // Then call the base handler if needed
        return await baseHandler.Handle(request, cancellationToken);
    }
}