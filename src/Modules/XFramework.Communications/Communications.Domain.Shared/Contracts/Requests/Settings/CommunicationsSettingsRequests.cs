namespace Communications.Domain.Shared.Contracts.Requests.Settings;

[MemoryPackable]
public partial record GetCommunicationsSettingsRequest : RequestBase,
    IQuery<QueryResponse<CommunicationsSettingsResponse>>,
    IBoltRequest<GetCommunicationsSettingsRequest, QueryResponse<CommunicationsSettingsResponse>>;

[MemoryPackable]
public partial record UpdateCommunicationsSettingsRequest : RequestBase,
    ICommand<CmdResponse<CommunicationsSettingsResponse>>,
    IBoltRequest<UpdateCommunicationsSettingsRequest, CmdResponse<CommunicationsSettingsResponse>>
{
    public List<UpdateCommunicationsSettingValueRequest> Values { get; set; } = [];
}

[MemoryPackable]
public partial record UpdateCommunicationsSettingValueRequest
{
    public string GroupName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
