using System.Collections;
using Microsoft.Extensions.Caching.Memory;
using XFramework.Core.DataAccess.Query;
using XFramework.Core.Services;
using XFramework.Integration.Abstractions;

namespace IdentityServer.Core.DataAccess.Query.Tenant;
using XFramework.Domain.Shared.Contracts;

public class GetTenant(
    ILogger<GetHandler<Tenant>> logger,
    DbContext dbContext,
    IMemoryCache cache,
    IHelperService helperService,
    IRequestHandler<Get<Tenant>, QueryResponse<Tenant>> baseHandler
)
    : IGetHandler<Tenant>, IDecorator
{
    public async Task<QueryResponse<Tenant>> Handle(Get<Tenant> request,
        CancellationToken cancellationToken)
    {
        IQueryable<Tenant> query = dbContext.Set<Tenant>();

        if (request.IncludeNavigations is true)
        {
            query = IncludeNavigations(query, 3); // Limited to 3 levels deep
        }

        // Use caching
        if (!cache.TryGetValue($"Entity-{nameof(Tenant)}-{request.Id}", out Tenant? entity))
        {
            entity = await query
                .Where(e => e.Id == request.Id)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            //entity = helperService.RemoveCircularReference(entity);

            if (entity is not null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    // Set the cache expiration as needed
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };

                //cache.Set($"Entity-{nameof(Tenant)}-{request.Id}", entity, cacheOptions);
            }
        }

        if (entity is null)
        {
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} was not found", nameof(Tenant), request.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }
        
        return new QueryResponse<Tenant>
        {
            Response = entity,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
    
    private IQueryable<Tenant> IncludeNavigations(IQueryable<Tenant> query, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth) return query;

        foreach (var property in typeof(Tenant).GetProperties())
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