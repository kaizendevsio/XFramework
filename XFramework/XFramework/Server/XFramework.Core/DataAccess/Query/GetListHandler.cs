using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Extensions;

namespace XFramework.Core.DataAccess.Query;

public class GetListHandler<TModel>(
        ILogger<GetListHandler<TModel>> logger,
        DbContext dbContext,
        CacheManager cache,
        ITenantService tenantService,
        IHelperService helperService
    ) 
    : IGetListHandler<TModel>
    where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
{
    private int _maxNavigationDepth = 1;

    public async Task<QueryResponse<PaginatedResult<TModel>>> Handle(GetList<TModel> request, CancellationToken cancellationToken)
    {
        var cacheKey = $"GetList-{typeof(TModel).Name}-{string.Join("-", request.Filter?.Select(i => $"{i.PropertyName}-{i.Operation}-{i.Value}"))}"; // Adjust cache key as needed
        
        if (request.NoCache is false)
        {
            var cachedResult = cache.Get<PaginatedResult<TModel>>(cacheKey);
            if (cachedResult is not null)
            {
                return new QueryResponse<PaginatedResult<TModel>>
                {
                    Response = cachedResult
                };
            }
        }

        _maxNavigationDepth = request.NavigationDepth > 0 
            ? request.NavigationDepth 
            : _maxNavigationDepth;
        
        if (request.TenantId is null && request.Metadata.TenantId is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "TenantId is required"
            };
        }
        
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId ?? request.TenantId);

        IQueryable<TModel> query = dbContext.Set<TModel>();

        if (request.IncludeNavigations.HasValue && request.IncludeNavigations.Value)
        {
            query = IncludeNavigations(query, _maxNavigationDepth);
        }

        if (request.Filter != null && request.Filter.Any())
        {
            var expression = request.Filter.ToExpression<TModel>();
            query = query.Where(expression);
        }

        query = query
            .AsSplitQuery()
            .AsNoTracking();

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Where(i => i.TenantId == tenant.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        
        items = helperService.RemoveCircularReference(items);

        var paginatedResult = new PaginatedResult<TModel>(totalItems, request.PageNumber, request.PageSize, items);

        #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        cache.Set(cacheKey, paginatedResult);

        return new QueryResponse<PaginatedResult<TModel>>
        {
            Response = paginatedResult
        };
    }

    private IQueryable<TModel> IncludeNavigations(IQueryable<TModel> query, int maxDepth, int currentDepth = 0, PropertyInfo? propertyInfo = null)
    {
        if (currentDepth >= maxDepth) return query;
        var model = propertyInfo?.PropertyType ?? typeof(TModel);

        foreach (var property in model.GetProperties().Where(p => p.PropertyType.IsClass && p.PropertyType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType)))
        {
            if (currentDepth >= maxDepth) return query;
            query = query.Include(property.Name);
            query = IncludeNavigations(query, maxDepth, currentDepth + 1, propertyInfo); // Recursive call
        }
        return query;
    }
}
