using System.Collections;
using Microsoft.Extensions.Caching.Memory;
using XFramework.Core.DataAccess.Query;
using XFramework.Integration.Abstractions;

namespace IdentityServer.Core.Query.Tenant;

public class GetTenant(
    ILogger<GetHandler<XFramework.Domain.Shared.Contracts.Tenant>> logger,
    DbContext dbContext,
    IMemoryCache cache,
    IHelperService helperService,
    IRequestHandler<Get<XFramework.Domain.Shared.Contracts.Tenant>, QueryResponse<XFramework.Domain.Shared.Contracts.Tenant>> baseHandler
)
    : IGetHandler<XFramework.Domain.Shared.Contracts.Tenant>, IDecorator
{
    public async Task<QueryResponse<XFramework.Domain.Shared.Contracts.Tenant>> Handle(Get<XFramework.Domain.Shared.Contracts.Tenant> request,
        CancellationToken cancellationToken)
    {
        IQueryable<XFramework.Domain.Shared.Contracts.Tenant> query = dbContext.Set<XFramework.Domain.Shared.Contracts.Tenant>();

        if (request.IncludeNavigations is true)
        {
            query = IncludeNavigations(query, 3); // Limited to 3 levels deep
        }

        // Use caching
        if (!cache.TryGetValue($"Entity-{nameof(XFramework.Domain.Shared.Contracts.Tenant)}-{request.Id}", out XFramework.Domain.Shared.Contracts.Tenant? entity))
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
            logger.LogWarning("Entity of type {EntityName} with ID {EntityId} was not found", nameof(XFramework.Domain.Shared.Contracts.Tenant), request.Id);
            throw new KeyNotFoundException("The requested item was not found");
        }
        
        return new QueryResponse<XFramework.Domain.Shared.Contracts.Tenant>
        {
            Response = entity,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
    
    private IQueryable<XFramework.Domain.Shared.Contracts.Tenant> IncludeNavigations(IQueryable<XFramework.Domain.Shared.Contracts.Tenant> query, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth) return query;

        foreach (var property in typeof(XFramework.Domain.Shared.Contracts.Tenant).GetProperties())
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