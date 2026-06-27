using Messaging.Domain.Shared.Contracts.Requests.Admin;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Admin;

public static class GetMessagingModerationRulesEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/admin/moderation/rules", Tags = ["Messaging Admin"],
        Summary = "Get Messaging moderation rules",
        Description = "Returns tenant-scoped Messaging moderation rules.")]
    public static Task<Result<GetMessagingModerationRulesResponse>> Handle(
        GetMessagingModerationRulesRequest request,
        IMessagingModerationService moderationService,
        CancellationToken ct) =>
        moderationService.GetRulesAsync(request, ct);
}

public static class CreateMessagingModerationRuleEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/admin/moderation/rules", Tags = ["Messaging Admin"],
        Summary = "Create Messaging moderation rule",
        Description = "Creates a tenant-scoped keyword or regex moderation rule.")]
    public static Task<Result<MessagingModerationRuleResponse>> Handle(
        CreateMessagingModerationRuleRequest request,
        IMessagingModerationService moderationService,
        CancellationToken ct) =>
        moderationService.CreateRuleAsync(request, ct);
}

public static class UpdateMessagingModerationRuleEndpoint
{
    [BoltHandler]
    [MapPut("/api/messaging/admin/moderation/rules/{ruleId:guid}", Tags = ["Messaging Admin"],
        Summary = "Update Messaging moderation rule",
        Description = "Updates a tenant-scoped moderation rule.")]
    public static Task<Result<MessagingModerationRuleResponse>> Handle(
        UpdateMessagingModerationRuleRequest request,
        IMessagingModerationService moderationService,
        CancellationToken ct) =>
        moderationService.UpdateRuleAsync(request, ct);
}

public static class DeleteMessagingModerationRuleEndpoint
{
    [BoltHandler]
    [MapDelete("/api/messaging/admin/moderation/rules/{ruleId:guid}", Tags = ["Messaging Admin"],
        Summary = "Delete Messaging moderation rule",
        Description = "Soft-deletes a tenant-scoped moderation rule.")]
    public static Task<Result<CmdResponse>> Handle(
        DeleteMessagingModerationRuleRequest request,
        IMessagingModerationService moderationService,
        CancellationToken ct) =>
        moderationService.DeleteRuleAsync(request, ct);
}

public static class ReviewMessageReportEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/admin/moderation/reports/{reportId:guid}/actions", Tags = ["Messaging Admin"],
        Summary = "Action Messaging report",
        Description = "Reviews, assigns, dismisses, resolves, escalates, or annotates a moderation report.")]
    public static Task<Result<MessagingReportWorkflowResponse>> Handle(
        ReviewMessageReportRequest request,
        IMessagingModerationService moderationService,
        CancellationToken ct) =>
        moderationService.ReviewReportAsync(request, ct);
}
