namespace XFramework.Integration.Abstractions;

public interface ICrudService<T> : IXFrameworkService 
    where T : class
{
    Task<CmdResponse<T>> Create(T entity);
    Task<CmdResponse<T>> Patch(T entity);
    Task<CmdResponse<T>> Replace(T entity);
    Task<CmdResponse> Delete(T entity);
    
    Task<QueryResponse<PaginatedResult<T>>> GetList(bool includeNavigations, IQueryable<T> filter);
    Task<QueryResponse<T>> Get(bool includeNavigations, Guid id);
    
}