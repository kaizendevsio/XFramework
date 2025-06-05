using Inventario.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;

namespace Inventario.Integration.Drivers;

public partial interface IInventarioServiceWrapper
{
    public Task<CmdResponse> SampleMethod(SampleMethodRequest request);
}

public partial record InventarioServiceWrapper : IInventarioServiceWrapper
{
    public Task<CmdResponse> SampleMethod(SampleMethodRequest request)
    {
        return SendVoidAsync(request);
    }
}