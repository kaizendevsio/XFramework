using System.Linq.Expressions;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Domain.Generic.Contracts.Requests;

public record Create<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>
{
    public Create() : this(null)
    {
        
    }
}

public record Patch<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>
{
    public Patch() : this(null)
    {
        
    }
}

public record Replace<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>
{
    public Replace() : this(null)
    {
        
    }
}

public record Delete<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>
{
    public Delete() : this(null)
    {
        
    }
}

public record GetList<T>(
    int PageSize,
    int PageNumber,
    Guid? TenantId,
    bool? IncludeNavigations = false,
    List<QueryFilter>? Filter = null) : RequestBase, IRequest<QueryResponse<PaginatedResult<T>>>
{
    public GetList() : this(100, 1, null)
    {
        
    }
};

public record Get<T>(Guid Id, Guid? TenantId, bool? IncludeNavigations = null) : RequestBase, IRequest<QueryResponse<T>>
{
    public Get() : this(Guid.Empty, null)
    {
        
    }
}


public record RequestBase : IHasRequestServer
{
    public RequestMetadata Metadata { get; set; } = new ();
}