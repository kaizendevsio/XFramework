using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Core.DataAccess.Commands;

public class PatchHandler<TModel>(
        IMemoryCache cache,
        AppDbContext appDbContext,
        ILogger<PatchHandler<TModel>> logger,
        ITenantService tenantService
    )
    : IPatchHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{

    public async Task<CmdResponse<TModel>> Handle(Patch<TModel> request, CancellationToken cancellationToken)
    {
        if (request.Model?.Id is null)
        {
            logger.LogWarning("Patch attempt with null ID for {EntityName}", typeof(TModel).Name);
            throw new ArgumentException("An error occurred while processing your request");
        }
        
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId ?? request.Model.TenantId);

        var entity = await appDbContext.Set<TModel>()
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.Id == request.Model.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} not found during patching attempt", typeof(TModel).Name, request.Model.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }

        entity.Adapt(request.Model);
        entity.ModifiedAt = DateTime.UtcNow;

        if (entity is IHasConcurrencyStamp concurrencyEntity)
        {
            concurrencyEntity.ConcurrencyStamp = Guid.NewGuid();
        }

        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);

            // Remove the entity from the cache after successful patch
            cache.Remove($"Entity-{typeof(TModel).Name}-{request.Model.Id}");

            logger.LogInformation("Entity of type {EntityName} with ID {EntityId} successfully patched", typeof(TModel).Name, request.Model.Id);

            return new CmdResponse<TModel>
            {
                Response = entity,
                HttpStatusCode = HttpStatusCode.OK
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict while patching entity of type {EntityName} with ID {EntityId}", typeof(TModel).Name, request.Model.Id);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while patching entity of type {EntityName} with ID {EntityId}", typeof(TModel).Name, request.Model.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}
