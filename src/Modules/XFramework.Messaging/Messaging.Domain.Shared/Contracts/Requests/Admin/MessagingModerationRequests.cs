namespace Messaging.Domain.Shared.Contracts.Requests.Admin;

using Messaging.Domain.Shared;

using TGetRulesRequest = GetMessagingModerationRulesRequest;
using TGetRulesResponse = QueryResponse<GetMessagingModerationRulesResponse>;
using TCreateRuleRequest = CreateMessagingModerationRuleRequest;
using TCreateRuleResponse = CmdResponse<MessagingModerationRuleResponse>;
using TUpdateRuleRequest = UpdateMessagingModerationRuleRequest;
using TUpdateRuleResponse = CmdResponse<MessagingModerationRuleResponse>;
using TDeleteRuleRequest = DeleteMessagingModerationRuleRequest;
using TReviewReportRequest = ReviewMessageReportRequest;
using TReviewReportResponse = CmdResponse<MessagingReportWorkflowResponse>;

[MemoryPackable]
public partial record GetMessagingModerationRulesRequest : RequestBase,
    IQuery<TGetRulesResponse>,
    IBoltRequest<TGetRulesRequest, TGetRulesResponse>
{
    public bool IncludeInactive { get; set; }
}

[MemoryPackable]
public partial record CreateMessagingModerationRuleRequest : RequestBase,
    ICommand<TCreateRuleResponse>,
    IBoltRequest<TCreateRuleRequest, TCreateRuleResponse>
{
    public string Name { get; set; } = string.Empty;
    public string MatchType { get; set; } = MessageModerationRuleMatchTypes.Keyword;
    public string Pattern { get; set; } = string.Empty;
    public string Action { get; set; } = MessageModerationRuleActions.Flag;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

[MemoryPackable]
public partial record UpdateMessagingModerationRuleRequest : RequestBase,
    ICommand<TUpdateRuleResponse>,
    IBoltRequest<TUpdateRuleRequest, TUpdateRuleResponse>
{
    public Guid RuleId { get; set; }
    public string? Name { get; set; }
    public string? MatchType { get; set; }
    public string? Pattern { get; set; }
    public string? Action { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
}

[MemoryPackable]
public partial record DeleteMessagingModerationRuleRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TDeleteRuleRequest, CmdResponse>
{
    public Guid RuleId { get; set; }
}

[MemoryPackable]
public partial record ReviewMessageReportRequest : RequestBase,
    ICommand<TReviewReportResponse>,
    IBoltRequest<TReviewReportRequest, TReviewReportResponse>
{
    public Guid ReportId { get; set; }
    public string Action { get; set; } = MessageReportAuditActions.Reviewed;
    public Guid? AssignedCredentialId { get; set; }
    public string? Note { get; set; }
}
