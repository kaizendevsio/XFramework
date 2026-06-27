namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record MessageTemplateResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateType { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public List<string> RequiredVariables { get; set; } = [];
    public Guid? OwnerCredentialId { get; set; }
    public string? OwnerLabel { get; set; }
    public bool IsDefault { get; set; }
    public bool IsLocked { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? SystemReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

[MemoryPackable]
public partial record GetMessageTemplatesResponse
{
    public List<MessageTemplateResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

[MemoryPackable]
public partial record RenderMessageTemplateResponse
{
    public Guid TemplateId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> TemplateVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
