namespace XFramework.Integration.Interfaces;

public interface ICrudService : IXFrameworkService
{
    List<T> GetList<T, TR>(TR entity) where T : class;
    T Get<T, TR>(TR entity) where T : class;
    T Verify<T, TR>(TR entity) where T : class;
    Task<CmdResponse<T>> Create<T>(T entity) where T : class;
    Task<CmdResponse<T>> Update<T>(T entity) where T : class;
    Task<CmdResponse> Delete<T>(T entity) where T : class;
}