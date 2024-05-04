using MediatR;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Domain.Shared.Contracts.Requests;

public interface ICommand : IRequest<CmdResponse>;
public interface ICommand<T> : IRequest<CmdResponse<T>>;
public interface IQuery<T> : IRequest<QueryResponse<T>>;

public record Create<T>(T Model) : RequestBase, ICommand<T>;

public record Patch<T>(T Model) : RequestBase, ICommand<T>;

public record Replace<T>(T Model) : RequestBase, ICommand<T>;

public record Delete<T>(T Model) : RequestBase, ICommand<T>;

public record GetList<T>(
    int PageSize,
    int PageNumber,
    Guid? TenantId,
    bool NoCache = true,
    int NavigationDepth = 1,
    bool? IncludeNavigations = false,
    List<QueryFilter>? Filter = null,
    List<string>? Includes = null) : RequestBase, IQuery<PaginatedResult<T>>;

public record Get<T>(
    Guid Id, 
    Guid? TenantId, 
    bool NoCache = true, 
    int NavigationDepth = 1,
    bool? IncludeNavigations = null,
    List<string>? Includes = null) : RequestBase, IQuery<T>;

public record RequestBase : IHasRequestServer
{
    public RequestMetadata Metadata { get; set; } = new ();
}