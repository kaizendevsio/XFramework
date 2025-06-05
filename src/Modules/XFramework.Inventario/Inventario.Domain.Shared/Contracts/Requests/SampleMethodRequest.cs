using MediatR;
using MemoryPack;
using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Inventario.Domain.Shared.Contracts.Requests;

using TRequest = SampleMethodRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record SampleMethodRequest : RequestBase, 
    IRequest<TResponse>, 
    IStreamflowRequest<TRequest, TResponse>
{
    public string? SomeParameterToPass { get; set; }
}