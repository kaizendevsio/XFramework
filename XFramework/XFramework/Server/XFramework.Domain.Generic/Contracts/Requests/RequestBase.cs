﻿using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Domain.Generic.Contracts.Requests;

public record Create<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>;

public record Patch<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>;

public record Replace<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>;

public record Delete<T>(T Model) : RequestBase, IRequest<CmdResponse<T>>;

public record GetList<T>(
    int PageSize,
    int PageNumber,
    Guid? TenantId,
    bool? IncludeNavigations = false,
    List<QueryFilter>? Filter = null) : RequestBase, IRequest<QueryResponse<PaginatedResult<T>>>;

public record Get<T>(Guid Id, Guid? TenantId, bool? IncludeNavigations = null) : RequestBase, IRequest<QueryResponse<T>>;

public record RequestBase : IHasRequestServer
{
    public RequestMetadata Metadata { get; set; } = new ();
}