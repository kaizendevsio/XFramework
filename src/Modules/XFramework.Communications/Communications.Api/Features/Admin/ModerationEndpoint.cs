using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Admin;

public static class GetCommunicationsModerationRulesEndpoint
{
    [BoltHandler]
    [MapGet("/api/communications/admin/moderation/rules", Tags = ["Communications Admin"],
        Summary = "Get Communications moderation rules",
        Description = "Returns tenant-scoped Communications moderation rules.")]
    public static Task<Result<GetCommunicationsModerationRulesResponse>> Handle(
        GetCommunicationsModerationRulesRequest request,
        ICommunicationsModerationService moderationService,
        CancellationToken ct) =>
        moderationService.GetRulesAsync(request, ct);
}

public static class CreateCommunicationsModerationRuleEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/admin/moderation/rules", Tags = ["Communications Admin"],
        Summary = "Create Communications moderation rule",
        Description = "Creates a tenant-scoped keyword or regex moderation rule.")]
    public static Task<Result<CommunicationsModerationRuleResponse>> Handle(
        CreateCommunicationsModerationRuleRequest request,
        ICommunicationsModerationService moderationService,
        CancellationToken ct) =>
        moderationService.CreateRuleAsync(request, ct);
}

public static class UpdateCommunicationsModerationRuleEndpoint
{
    [BoltHandler]
    [MapPut("/api/communications/admin/moderation/rules/{ruleId:guid}", Tags = ["Communications Admin"],
        Summary = "Update Communications moderation rule",
        Description = "Updates a tenant-scoped moderation rule.")]
    public static Task<Result<CommunicationsModerationRuleResponse>> Handle(
        UpdateCommunicationsModerationRuleRequest request,
        ICommunicationsModerationService moderationService,
        CancellationToken ct) =>
        moderationService.UpdateRuleAsync(request, ct);
}

public static class DeleteCommunicationsModerationRuleEndpoint
{
    [BoltHandler]
    [MapDelete("/api/communications/admin/moderation/rules/{ruleId:guid}", Tags = ["Communications Admin"],
        Summary = "Delete Communications moderation rule",
        Description = "Soft-deletes a tenant-scoped moderation rule.")]
    public static Task<Result<CmdResponse>> Handle(
        DeleteCommunicationsModerationRuleRequest request,
        ICommunicationsModerationService moderationService,
        CancellationToken ct) =>
        moderationService.DeleteRuleAsync(request, ct);
}

public static class ReviewMessageReportEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/admin/moderation/reports/{reportId:guid}/actions", Tags = ["Communications Admin"],
        Summary = "Action Communications report",
        Description = "Reviews, assigns, dismisses, resolves, escalates, or annotates a moderation report.")]
    public static Task<Result<CommunicationsReportWorkflowResponse>> Handle(
        ReviewMessageReportRequest request,
        ICommunicationsModerationService moderationService,
        CancellationToken ct) =>
        moderationService.ReviewReportAsync(request, ct);
}
