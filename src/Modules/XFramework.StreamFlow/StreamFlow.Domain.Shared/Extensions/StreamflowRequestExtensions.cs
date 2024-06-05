using MediatR;
using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace StreamFlow.Domain.Shared.Extensions;

/// <summary>
/// Extends XFramwork's ICommand with specific response to inherit RequestBase and support IStreamflowRequest and register this as streamflow request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract record XCommand<TRequest, TResponse> : RequestBase, ICommand<TResponse>,
    IStreamflowRequest<TRequest, CmdResponse<TResponse>>;

/// <summary>
/// Extends XFramwork's ICommand to inherit RequestBase and support IStreamflowRequest and register this as streamflow request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
public abstract record XCommand<TRequest> : RequestBase, ICommand,
    IStreamflowRequest<TRequest, CmdResponse>;

/// <summary>
/// Extends XFramwork's IQuery with specific response to inherit RequestBase and support IStreamflowRequest and register this as streamflow request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract record XQuery<TRequest, TResponse> : RequestBase, IQuery<TResponse>,
    IStreamflowRequest<TRequest, QueryResponse<TResponse>>;