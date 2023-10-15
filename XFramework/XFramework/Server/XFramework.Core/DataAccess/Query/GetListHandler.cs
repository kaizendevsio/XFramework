using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace XFramework.Core.DataAccess.Query;

public class GetListHandler<TModel>(
        ILogger<GetListHandler<TModel>> logger,
        AppDbContext appDbContext,
        IMemoryCache cache
    ) 
    : IGetListHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{
    private const int MaxNavigationDepth = 3;

    public async Task<QueryResponse<PaginatedResult<TModel>>> Handle(GetList<TModel> request, CancellationToken cancellationToken)
    {
        var cacheKey = $"GetList-{typeof(TModel).Name}-{request.Filter}";
            
        if (cache.TryGetValue(cacheKey, out PaginatedResult<TModel>? cachedResult))
        {
            return new QueryResponse<PaginatedResult<TModel>>
            {
                Response = cachedResult
            };
        }
        
        var tenant = await GetTenant(request.TenantId);

        IQueryable<TModel> query = appDbContext.Set<TModel>();

        if (request.IncludeNavigations.HasValue && request.IncludeNavigations.Value)
        {
            query = IncludeNavigations(query, MaxNavigationDepth);
        }

        if (request.Filter != null)
        {
            query = query.Where(request.Filter);
        }

        query = query.AsSplitQuery();

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.PageNumber - 1) * request.PageSize)
            .Where(i => i.TenantId == tenant.Id)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var paginatedResult = new PaginatedResult<TModel>(totalItems, request.PageNumber, request.PageSize, items);

            
        cache.Set(cacheKey, paginatedResult, TimeSpan.FromMinutes(10)); // Adjust cache duration as needed

        return new QueryResponse<PaginatedResult<TModel>>
        {
            Response = paginatedResult
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
