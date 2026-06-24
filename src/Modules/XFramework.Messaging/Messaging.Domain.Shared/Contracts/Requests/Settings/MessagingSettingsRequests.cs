namespace Messaging.Domain.Shared.Contracts.Requests.Settings;

[MemoryPackable]
public partial record GetMessagingSettingsRequest : RequestBase,
    IQuery<QueryResponse<MessagingSettingsResponse>>,
    IBoltRequest<GetMessagingSettingsRequest, QueryResponse<MessagingSettingsResponse>>;

[MemoryPackable]
public partial record UpdateMessagingSettingsRequest : RequestBase,
    ICommand<CmdResponse<MessagingSettingsResponse>>,
    IBoltRequest<UpdateMessagingSettingsRequest, CmdResponse<MessagingSettingsResponse>>
{
    public List<UpdateMessagingSettingValueRequest> Values { get; set; } = [];
}

[MemoryPackable]
public partial record UpdateMessagingSettingValueRequest
{
    public string GroupName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
