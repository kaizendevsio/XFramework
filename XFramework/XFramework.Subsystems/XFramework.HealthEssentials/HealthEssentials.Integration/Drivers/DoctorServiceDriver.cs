using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;

namespace HealthEssentials.Integration.Drivers;

public class DoctorServiceDriver : ICrudService
{
    public List<T> GetList<T, TR>(TR entity) where T : class
    {
        throw new NotImplementedException();
    }

    public T Get<T, TR>(TR entity) where T : class
    {
        throw new NotImplementedException();
    }

    public T Verify<T, TR>(TR entity) where T : class
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<T>> Create<T>(T entity) where T : class
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse<T>> Update<T>(T entity) where T : class
    {
        throw new NotImplementedException();
    }

    public async Task<CmdResponse> Delete<T>(T entity) where T : class
    {
        throw new NotImplementedException();
    }
}