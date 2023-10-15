﻿using System.Linq.Expressions;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Core.DataAccess.Query;

public record GetList<T>(int PageSize, int PageNumber, Guid TenantId, bool? IncludeNavigations = false, Expression<Func<T, bool>>? Filter = null) : RequestBase, IRequest<QueryResponse<PaginatedResult<T>>>;
public record Get<T>(Guid Id, Guid TenantId, bool? IncludeNavigations = null) : RequestBase, IRequest<QueryResponse<T>>;


public static class XQuery
{
    public static GetList<TModel> GetList<TModel>(Guid tenantId, int pageNumber = 1, int pageSize = 100, bool? includeNavigations = false, Expression<Func<TModel, bool>>? filter = null) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new GetList<TModel>(pageSize, pageNumber, tenantId, includeNavigations, filter);
    }
    
    public static Get<TModel> Get<TModel>(Guid id, Guid tenantId, bool? includeNavigations = null) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new Get<TModel>(id, tenantId, includeNavigations);
    }
}

public interface IGetHandler<TModel> : IRequestHandler<Get<TModel>, QueryResponse<TModel>>;
public interface IGetListHandler<TModel> : IRequestHandler<GetList<TModel>, QueryResponse<PaginatedResult<TModel>>>;

