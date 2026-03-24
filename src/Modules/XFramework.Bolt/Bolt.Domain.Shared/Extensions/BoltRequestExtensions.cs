using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Bolt.Domain.Shared.Extensions;

/// <summary>
/// Extends XFramwork's ICommand with specific response to inherit RequestBase and support IBoltRequest and register this as bolt request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract record XCommand<TRequest, TResponse> : RequestBase, ICommand<TResponse>,
    IBoltRequest<TRequest, CmdResponse<TResponse>>;

/// <summary>
/// Extends XFramwork's ICommand to inherit RequestBase and support IBoltRequest and register this as bolt request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
public abstract record XCommand<TRequest> : RequestBase, ICommand<CmdResponse>,
    IBoltRequest<TRequest, CmdResponse>;

/// <summary>
/// Extends XFramwork's IQuery with specific response to inherit RequestBase and support IBoltRequest and register this as bolt request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract record XQuery<TRequest, TResponse> : RequestBase, IQuery<TResponse>,
    IBoltRequest<TRequest, QueryResponse<TResponse>>;