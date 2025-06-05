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

public class Replace(
    DbContext appDbContext,
    ILogger<Replace> logger,
    ITenantService tenantService,
    IRequestHandler<Replace<TEntity>, CmdResponse<TEntity>> baseHandler
)
    : IReplaceHandler<TEntity>, IDecorator
{
    public async Task<CmdResponse<TEntity>> Handle(Replace<TEntity> request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        // Then call the base handler if needed
        return await baseHandler.Handle(request, cancellationToken);
    }
}