﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Services;

namespace XFramework.Core.DataAccess.Commands;

public class DeleteHandler<TModel>(
        DbContext dbContext,
        ILogger<DeleteHandler<TModel>> logger,
        ITenantService tenantService
    ) 
    : IDeleteHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{

    public async Task<CmdResponse> Handle(Delete<TModel> request, CancellationToken cancellationToken)
    {
        if (request.Model?.Id is null)
        {
            logger.LogWarning("Delete attempt with null ID for {EntityName}", typeof(TModel).Name);
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

        // Fetch the entity by its ID.
        var entity = await dbContext.Set<TModel>()
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.Id == request.Model.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} not found during deletion attempt", typeof(TModel).Name, request.Model.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }

        // Set the properties for soft delete
        if (entity is ISoftDeletable softDeletableEntity)
        {
            softDeletableEntity.IsDeleted = true;
            softDeletableEntity.IsEnabled = false;
            softDeletableEntity.DeletedAt = DateTime.UtcNow;
        }
        else
        {
            logger.LogError("Entity type {EntityName} does not implement ISoftDeletable interface. Entity ID: {EntityId}", typeof(TModel).Name, request.Model.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }

        try
        {
            // Save the changes.
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Entity of type {EntityName} with ID {EntityId} successfully soft-deleted", typeof(TModel).Name, request.Model.Id);
   
            // Remove the entity from the cache after successful deletion
            //await cache.InvalidateCacheForModel(request.Model);
            //cache.Remove($"GetList-{typeof(TModel).Name}-");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while soft-deleting entity of type {EntityName} with ID {EntityId}", typeof(TModel).Name, request.Model.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }


        // Return a successful response.
        return new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Entity successfully deleted"
        };
    }
}
