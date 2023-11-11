using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Core.DataAccess.Query;

public class GetHandler<TModel>(
        ILogger<GetHandler<TModel>> logger,
        AppDbContext appDbContext,
        IMemoryCache cache
    )
    : IGetHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{

    public async Task<QueryResponse<TModel>> Handle(Get<TModel> request, CancellationToken cancellationToken)
    {
        IQueryable<TModel> query = appDbContext.Set<TModel>();

        if (request.IncludeNavigations is true)
        {
            query = IncludeNavigations(query, 3); // Limited to 3 levels deep
        }
        
        var tenant = await GetTenant(request.TenantId);

        // Use caching
        if (!cache.TryGetValue($"Entity-{typeof(TModel).Name}-{request.Id}", out TModel? entity))
        {
            entity = await query
                .Where(i => i.TenantId == tenant.Id)
                .Where(e => e.Id == request.Id)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is not null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    // Set the cache expiration as needed
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };

                cache.Set($"Entity-{typeof(TModel).Name}-{request.Id}", entity, cacheOptions);
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

