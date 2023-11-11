using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Core.DataAccess.Commands;

public class ReplaceHandler<TModel>(
        IMemoryCache cache,
        AppDbContext appDbContext,
        ILogger<ReplaceHandler<TModel>> logger
    )
    : IReplaceHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{

    public async Task<CmdResponse<TModel>> Handle(Replace<TModel> request, CancellationToken cancellationToken)
    {
        if (request.Model?.Id is null)
        {
            logger.LogWarning("Replace attempt with null ID for {EntityName}", typeof(TModel).Name);
            throw new ArgumentException("An error occurred while processing your request");
        }
        
        var tenant = await GetTenant(request.Model.TenantId);

        // Fetch the existing entity by its ID.
        var existingEntity = await appDbContext.Set<TModel>()
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.Id == request.Model.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEntity == null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} was not found during replace attempt", typeof(TModel).Name, request.Model.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }

        // Adapt (map) the provided model data over the existing entity, while preserving the ID.
        request.Model.Adapt(existingEntity);
            
        // Set ModifiedAt and ConcurrencyStamp
        existingEntity.ModifiedAt = DateTime.UtcNow;
        existingEntity.ConcurrencyStamp = Guid.NewGuid();

        try
        {
            // Save the changes.
            await appDbContext.SaveChangesAsync(cancellationToken);
                
            // Remove the entity from the cache after successful patch
            cache.Remove($"Entity-{typeof(TModel).Name}-{request.Model.Id}");
                
            logger.LogInformation("Entity of type {EntityName} with ID {EntityId} was successfully replaced", typeof(TModel).Name, request.Model.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogError("Concurrency conflict occurred while replacing entity of type {EntityName} with ID {EntityId}", typeof(TModel).Name, request.Model.Id);
            throw new InvalidOperationException("A concurrency conflict occurred while processing your request");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while replacing entity of type {EntityName} with ID {EntityId}", typeof(TModel).Name, request.Model.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }

        // Return a successful response.
        return new CmdResponse<TModel>
        {
            Response = existingEntity,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
    
    private async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        var entity = await appDbContext.Tenants
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

        ArgumentNullException.ThrowIfNull(entity, "Tenant");
        
        return entity;
    }
}
