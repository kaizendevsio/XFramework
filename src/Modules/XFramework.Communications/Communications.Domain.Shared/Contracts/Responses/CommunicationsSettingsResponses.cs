using Communications.Domain.Shared;

namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CommunicationsSettingsResponse
{
    public Guid TenantId { get; set; }
    public List<CommunicationsSettingGroupResponse> Groups { get; set; } = [];
    public DateTime LoadedAtUtc { get; set; }
}

[MemoryPackable]
public partial record CommunicationsSettingGroupResponse
{
    public string SectionKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CommunicationsSettingValueResponse> Settings { get; set; } = [];
}

[MemoryPackable]
public partial record CommunicationsSettingValueResponse
{
    public string SectionKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string Source { get; set; } = CommunicationsSettingSources.Default;
    public CommunicationsSettingValueKind ValueKind { get; set; }
    public List<string> Options { get; set; } = [];
    public DateTime? LastUpdated { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
}

public static class CommunicationsSettingSources
{
    public const string Default = "Default";
    public const string Stored = "Stored";
}
