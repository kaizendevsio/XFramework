using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Abstractions;

namespace XFramework.Core.DataAccess.Query;

public class GetHandler<TModel>(
        ILogger<GetHandler<TModel>> logger,
        DbContext dbContext,
        CacheManager cache,
        ITenantService tenantService,
        IHelperService helperService
    )
    : IGetHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{

    private const int MaxNavigationDepth = 1;

    public async Task<QueryResponse<TModel>> Handle(Get<TModel> request, CancellationToken cancellationToken)
    {
        IQueryable<TModel> query = dbContext.Set<TModel>();

        if (request.IncludeNavigations is true)
        {
            query = IncludeNavigations(query, MaxNavigationDepth); // Limited to 3 levels deep
        }
        
        if (request.TenantId is null && request.Metadata.TenantId is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "TenantId is required"
            };
        }
        
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId ?? request.TenantId);

        // Use caching
        var entity = cache.Get<TModel>($"Entity-{typeof(TModel).Name}-{request.Id}");
        if (entity is null)
        {
            entity = await query
                .Where(i => i.TenantId == tenant.Id)
                .Where(e => e.Id == request.Id)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            entity = helperService.RemoveCircularReference(entity);

            if (entity is not null)
            {
                #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                cache.Set($"Entity-{typeof(TModel).Name}-{request.Id}", entity);
            }
        }

        if (entity is null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} was not found", typeof(TModel).Name, request.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }
        
        return new QueryResponse<TModel>
        {
            Response = entity,
            HttpStatusCode = HttpStatusCode.OK
        };
    }

    private IQueryable<TModel> IncludeNavigations(IQueryable<TModel> query, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth) return query;

        foreach (var property in typeof(TModel).GetProperties())
        {
            var propertyType = property.PropertyType;
            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
            {
                query = query.Include(property.Name);
                query = IncludeNavigations(query, maxDepth, currentDepth + 1); // Recursive call
            }
        }
        return query;
    }
}

