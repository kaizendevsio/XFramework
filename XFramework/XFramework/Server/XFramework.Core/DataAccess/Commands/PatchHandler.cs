using System.Collections;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TypeSupport.Extensions;
using XFramework.Core.Services;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Core.DataAccess.Commands;

public class PatchHandler<TModel>(
        DbContext dbContext,
        CacheManager cache,
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
        
        if (request.Metadata.TenantId is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "TenantId is required"
            };
        }
        
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId ?? request.Model.TenantId);
        
        var entity = await dbContext.Set<TModel>()
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.Id == request.Model.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} not found during patching attempt", typeof(TModel).Name, request.Model.Id);
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
        
        entity = request.Model.Adapt(entity);
        entity.ModifiedAt = DateTime.UtcNow;

        if (entity is IHasConcurrencyStamp concurrencyEntity)
        {
            concurrencyEntity.ConcurrencyStamp = Guid.NewGuid();
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // Remove the entity from the cache after successful patch
            await cache.InvalidateCacheForModel(request.Model);
            cache.Remove($"GetList-{typeof(TModel).Name}-");
            
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
    
    private bool IsNavigationProperty(Type type)
    {
        return (type.IsClass && type != typeof(string) && type != typeof(byte[])) ||
               (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string) &&
                (type.GetGenericArguments().Any() ? type.GetGenericArguments()[0].IsClass : false));
    }
}
