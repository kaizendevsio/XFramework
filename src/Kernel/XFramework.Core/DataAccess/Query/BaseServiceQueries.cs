using System.Linq.Expressions;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Core.DataAccess.Query;

public static class XQuery
{
    public static GetList<TModel> GetList<TModel>(
        Guid tenantId, 
        int pageNumber = 1, 
        int pageSize = 100, 
        bool noCache = false, 
        bool? includeNavigations = false, 
        int navigationDepth = 1,
        List<QueryFilter>? filter = null,
        List<string>? includes = null) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new GetList<TModel>(
            PageSize: pageSize, 
            PageNumber: pageNumber, 
            TenantId: tenantId, 
            NoCache:noCache, 
            IncludeNavigations: includeNavigations,
            NavigationDepth: navigationDepth,
            Filter: filter,
            Includes: includes);
    }
    
    public static Get<TModel> Get<TModel>(
        Guid id, 
        Guid tenantId, 
        bool? includeNavigations = null, 
        int navigationDepth = 1,
        bool noCache = false,
        List<string>? includes = null) 
        where TModel : class, IHasId, IAuditable, IHasConcurrencyStamp, ISoftDeletable, IHasTenantId
    {
        return new Get<TModel>(
            Id: id, 
            TenantId: tenantId, 
            NoCache: noCache, 
            NavigationDepth: navigationDepth,
            IncludeNavigations: includeNavigations,
            Includes: includes);
    }
}

public interface IGetHandler<TModel> : IRequestHandler<Get<TModel>, QueryResponse<TModel>>;
public interface IGetListHandler<TModel> : IRequestHandler<GetList<TModel>, QueryResponse<PaginatedResult<TModel>>>;

