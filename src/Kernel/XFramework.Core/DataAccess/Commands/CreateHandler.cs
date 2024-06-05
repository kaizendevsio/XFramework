using System.Collections;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Interfaces;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Services;

namespace XFramework.Core.DataAccess.Commands;

public class CreateHandler<TModel>(
        DbContext dbContext,
        CacheManager cache,
        ILogger<CreateHandler<TModel>> logger,
        ITenantService tenantService
    )
    : ICreateHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{
    
    public async Task<CmdResponse<TModel>> Handle(Create<TModel> request, CancellationToken cancellationToken)
    {
        if (request.Model is null)
        {
            logger.LogWarning("Attempt to create an entity of type {EntityName} with a null model", typeof(TModel).Name);
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

        // Set the CreatedAt property to the current UTC time.
        request.Model.Id = request.Model.Id != Guid.Empty ? request.Model.Id : Guid.NewGuid();
        request.Model.CreatedAt = DateTime.UtcNow;
        request.Model.TenantId = tenant.Id;

        // strip navigation properties
        var navigationProperties = request.Model.GetType().GetProperties()
            .Where(p => IsNavigationProperty(p.PropertyType))
            .ToList();

        foreach (var navigationProperty in navigationProperties.Where(navigationProperty => navigationProperty.CanWrite))
        {
            navigationProperty.SetValue(request.Model, null);
        }
        
        try
        {
            // Add the entity to the context.
            await dbContext.Set<TModel>().AddAsync(request.Model, cancellationToken);

            // Save the changes.
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Entity of type {EntityName} was successfully created", typeof(TModel).Name);
            
            //await cache.InvalidateCacheForModel(request.Model);
            //cache.Remove($"GetList-{typeof(TModel).Name}-");

            // Return a successful response.
            return new CmdResponse<TModel>
            {
                Response = request.Model,
                HttpStatusCode = HttpStatusCode.OK
                // ... Add other necessary properties or feedback.
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while creating entity of type {EntityName}", typeof(TModel).Name);
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