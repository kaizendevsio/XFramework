namespace Messaging.Domain.Shared.Contracts.Requests.Templates;

using Messaging.Domain.Shared;

using TCreateRequest = CreateMessageTemplateRequest;
using TCreateResponse = CmdResponse<MessageTemplateResponse>;
using TDeleteRequest = DeleteMessageTemplateRequest;
using TGetRequest = GetMessageTemplateRequest;
using TGetResponse = QueryResponse<MessageTemplateResponse>;
using TListRequest = GetMessageTemplatesRequest;
using TListResponse = QueryResponse<GetMessageTemplatesResponse>;
using TRenderRequest = RenderMessageTemplateRequest;
using TRenderResponse = QueryResponse<RenderMessageTemplateResponse>;
using TUpdateRequest = UpdateMessageTemplateRequest;
using TUpdateResponse = CmdResponse<MessageTemplateResponse>;
using TCloneRequest = CloneMessageTemplateRequest;
using TCloneResponse = CmdResponse<MessageTemplateResponse>;

[MemoryPackable]
public partial record GetMessageTemplatesRequest : RequestBase,
    IQuery<TListResponse>,
    IBoltRequest<TListRequest, TListResponse>
{
    public string? TemplateType { get; set; }
    public Guid? OwnerCredentialId { get; set; }
    public string? Search { get; set; }
    public bool IncludeInactive { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 20;
}

[MemoryPackable]
public partial record GetMessageTemplateRequest : RequestBase,
    IQuery<TGetResponse>,
    IBoltRequest<TGetRequest, TGetResponse>
{
    public Guid TemplateId { get; set; }
}

[MemoryPackable]
public partial record CreateMessageTemplateRequest : RequestBase,
    ICommand<TCreateResponse>,
    IBoltRequest<TCreateRequest, TCreateResponse>
{
    public string TemplateType { get; set; } = MessageTemplateTypes.Tenant;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<string> RequiredVariables { get; set; } = [];
    public Guid? OwnerCredentialId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; } = true;
}

[MemoryPackable]
public partial record UpdateMessageTemplateRequest : RequestBase,
    ICommand<TUpdateResponse>,
    IBoltRequest<TUpdateRequest, TUpdateResponse>
{
    public Guid TemplateId { get; set; }
    public string? Key { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public List<string>? RequiredVariables { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsEnabled { get; set; }
}

[MemoryPackable]
public partial record DeleteMessageTemplateRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TDeleteRequest, CmdResponse>
{
    public Guid TemplateId { get; set; }
}

[MemoryPackable]
public partial record CloneMessageTemplateRequest : RequestBase,
    ICommand<TCloneResponse>,
    IBoltRequest<TCloneRequest, TCloneResponse>
{
    public Guid TemplateId { get; set; }
    public string TemplateType { get; set; } = MessageTemplateTypes.Tenant;
    public Guid? OwnerCredentialId { get; set; }
    public string? Key { get; set; }
    public string? Name { get; set; }
}

[MemoryPackable]
public partial record RenderMessageTemplateRequest : RequestBase,
    IQuery<TRenderResponse>,
    IBoltRequest<TRenderRequest, TRenderResponse>
{
    public Guid? TemplateId { get; set; }
    public string? TemplateKey { get; set; }
    public Dictionary<string, string> TemplateVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
