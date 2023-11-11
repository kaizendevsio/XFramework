using System.Linq.Expressions;

namespace XFramework.Integration.Abstractions;

public interface ICrudService<T> : IXFrameworkService 
    where T : class
{
    Task<CmdResponse<T>> Create(T entity);
    Task<CmdResponse<T>> Patch(T entity);
    Task<CmdResponse<T>> Replace(T entity);
    Task<CmdResponse> Delete(T entity);
    
    Task<QueryResponse<PaginatedResult<T>>> GetList(int pageSize, int pageNumber, Guid? tenantId = null, bool? includeNavigations = false, List<QueryFilter>? filter = null);
    Task<QueryResponse<T>> Get(Guid id, Guid? tenantId = null, bool? includeNavigations = null);
    
}