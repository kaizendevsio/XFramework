using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Services;

namespace XFramework.Core.DataAccess.Commands;

public class ReplaceHandler<TModel>(
        DbContext dbContext,
        ILogger<ReplaceHandler<TModel>> logger,
        ITenantService tenantService
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
        
        if (request.Metadata.TenantId is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "TenantId is required"
            };
        }
        
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId ?? request.Model.TenantId);

        // Fetch the existing entity by its ID.
        var existingEntity = await dbContext.Set<TModel>()
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.Id == request.Model.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingEntity == null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} was not found during replace attempt", typeof(TModel).Name, request.Model.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }
        
        // strip navigation properties
        var navigationProperties = request.Model.GetType().GetProperties()
            .Where(p => IsNavigationProperty(p.PropertyType))
            .ToList();

        foreach (var navigationProperty in navigationProperties.Where(navigationProperty => navigationProperty.CanWrite))
        {
            navigationProperty.SetValue(request.Model, null);
        }

        // Adapt (map) the provided model data over the existing entity, while preserving the ID.
        existingEntity = request.Model;
            
        // Set ModifiedAt and ConcurrencyStamp
        existingEntity.ModifiedAt = DateTime.UtcNow;
        existingEntity.ConcurrencyStamp = Guid.NewGuid();

        try
        {
            // Save the changes.
            await dbContext.SaveChangesAsync(cancellationToken);
                
            // Remove the entity from the cache after successful patch
            //await cache.InvalidateCacheForModel(request.Model);
            //cache.Remove($"GetList-{typeof(TModel).Name}-");
                
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
    
    private bool IsNavigationProperty(Type type)
    {
        return (type.IsClass && type != typeof(string) && type != typeof(byte[])) ||
               (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string) &&
                (type.GetGenericArguments().Any() ? type.GetGenericArguments()[0].IsClass : false));
    }
}
