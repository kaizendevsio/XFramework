using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Core.Services;
using TEntity = Domain.Shared.Contracts.Service;

public class Delete(DbContext appDbContext,
    ILogger<Delete> logger,
    ITenantService tenantService,
    IRequestHandler<Delete<TEntity>, CmdResponse> baseHandler
)
    : IDeleteHandler<TEntity>, IDecorator
{
    public async Task<CmdResponse> Handle(Delete<TEntity> request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        // Then call the base handler if needed
        return await baseHandler.Handle(request, cancellationToken);
    }
}