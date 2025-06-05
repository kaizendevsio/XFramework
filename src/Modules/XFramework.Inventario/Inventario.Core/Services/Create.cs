using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.DataAccess.Commands;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Domain.Shared.Contracts;

namespace Inventario.Core.Services;
using TEntity = Domain.Shared.Contracts.Service;

public class Create(
    DbContext appDbContext,
    ILogger<Create> logger,
    ITenantService tenantService,
    IRequestHandler<Create<TEntity>, CmdResponse<TEntity>> baseHandler
)
    : ICreateHandler<TEntity>, IDecorator
{
    public async Task<CmdResponse<TEntity>> Handle(Create<TEntity> request, CancellationToken cancellationToken)
    {
        // Do custom stuff here...
        
        // Then call the base handler if needed
        return await baseHandler.Handle(request, cancellationToken);
    }
}