using Messaging.Domain.Shared;

namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record MessagingSettingsResponse
{
    public Guid TenantId { get; set; }
    public List<MessagingSettingGroupResponse> Groups { get; set; } = [];
    public DateTime LoadedAtUtc { get; set; }
}

[MemoryPackable]
public partial record MessagingSettingGroupResponse
{
    public string SectionKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<MessagingSettingValueResponse> Settings { get; set; } = [];
}

[MemoryPackable]
public partial record MessagingSettingValueResponse
{
    public string SectionKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string Source { get; set; } = MessagingSettingSources.Default;
    public MessagingSettingValueKind ValueKind { get; set; }
    public List<string> Options { get; set; } = [];
    public DateTime? LastUpdated { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
}

public static class MessagingSettingSources
{
    public const string Default = "Default";
    public const string Stored = "Stored";
}
