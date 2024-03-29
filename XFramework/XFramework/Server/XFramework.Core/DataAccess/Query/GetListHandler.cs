﻿using System.Collections;
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
        var cacheKey = $"GetList-{typeof(TModel).Name}-{string.Join("-", request.Filter?.Select(i => $"{i.PropertyName}-{i.Operation}-{i.Value}") ?? Array.Empty<string>())}"; // Adjust cache key as needed
        
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

        if (request.IncludeNavigations is true)
        {
            if (request.Includes is not null && request.Includes.Any())
            {
                query = request.Includes.Aggregate(query, (current, include) => current.Include(include));
            }
            else 
            {
                query = IncludeNavigations(query, typeof(TModel), _maxNavigationDepth); // Limited to 3 levels deep
            }
        }

        if (request.Filter != null && request.Filter.Any())
        {
            var expression = request.Filter.ToExpression<TModel>();
            query = query.Where(expression);
        }

        query = query
            .AsSplitQuery()
            .AsNoTracking();

        var items = await query
            .Where(i => i.TenantId == tenant.Id)
            .Where(i => i.IsDeleted == false)
            .OrderBy(i => i.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        
        items = helperService.RemoveCircularReference(items);

        var paginatedResult = new PaginatedResult<TModel>(items.Count, request.PageNumber, request.PageSize, items);

        #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        cache.Set(cacheKey, paginatedResult);

        return new QueryResponse<PaginatedResult<TModel>>
        {
            Response = paginatedResult
        };
    }

    private IQueryable<TModel> IncludeNavigations(IQueryable<TModel> query, Type model, int maxDepth, int currentDepth = 0, string? modelName = "")
    {
        if (currentDepth >= maxDepth || (typeof(TModel) == model && currentDepth > 0)) return query;

        // Collecting navigation properties to include
        var navigationProperties = model.GetProperties()
            .Where(p => IsNavigationProperty(p.PropertyType))
            .ToList();

        // Including navigation properties
        foreach (var property in navigationProperties)
        {
            if (typeof(TModel) == property.PropertyType)
            {
                continue;
            }
            query = currentDepth == 0
                ? query.Include(property.Name)
                : query.Include($"{modelName}.{property.Name}");
            query = IncludeNavigationsForProperty(query, model, property.Name, maxDepth, currentDepth);
        }

        return query;
    }

    private bool IsNavigationProperty(Type type)
    {
        return (type.IsClass && type != typeof(string) && type != typeof(byte[])) ||
               (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string) &&
                (type.GetGenericArguments().Any() ? type.GetGenericArguments()[0].IsClass : false));
    }

    private IQueryable<TModel> IncludeNavigationsForProperty(IQueryable<TModel> query, Type model, string propertyName, int maxDepth, int currentDepth)
    {
        var propertyType = model.GetProperty(propertyName).PropertyType;
        var isCollection = typeof(IEnumerable).IsAssignableFrom(propertyType);
        var elementType = isCollection ? propertyType.GetGenericArguments()[0] : propertyType;

        return IncludeNavigations(query, elementType, maxDepth, currentDepth + 1, modelName: propertyName);
    }
}
