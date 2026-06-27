using System.Net;
using System.Text.RegularExpressions;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Requests.Admin;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.DataContext;

namespace Messaging.Api.Services;

public sealed record MessagingModerationRuleMatch(
    Guid RuleId,
    string RuleName,
    string Action);

public interface IMessagingModerationService
{
    Task<IReadOnlyList<MessagingModerationRuleMatch>> EvaluateAsync(
        Guid tenantId,
        string text,
        CancellationToken ct = default);

    Task<Result<GetMessagingModerationRulesResponse>> GetRulesAsync(
        GetMessagingModerationRulesRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingModerationRuleResponse>> CreateRuleAsync(
        CreateMessagingModerationRuleRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingModerationRuleResponse>> UpdateRuleAsync(
        UpdateMessagingModerationRuleRequest request,
        CancellationToken ct = default);

    Task<Result<CmdResponse>> DeleteRuleAsync(
        DeleteMessagingModerationRuleRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingReportWorkflowResponse>> ReviewReportAsync(
        ReviewMessageReportRequest request,
        CancellationToken ct = default);
}

public sealed class MessagingModerationService(
    IDataContext dataContext,
    IMessagingRequestContextResolver requestContextResolver) : IMessagingModerationService
{
    public async Task<IReadOnlyList<MessagingModerationRuleMatch>> EvaluateAsync(
        Guid tenantId,
        string text,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(text))
            return [];

        var rules = await dataContext.Query<MessageModerationRule>()
            .Where(rule => rule.TenantId == tenantId)
            .Where(rule => !rule.IsDeleted && rule.IsEnabled)
            .ToListAsync(ct);

        return rules
            .Where(rule => IsMatch(rule, text))
            .Select(rule => new MessagingModerationRuleMatch(rule.Id, rule.Name, NormalizeAction(rule.Action)))
            .ToList();
    }

    public async Task<Result<GetMessagingModerationRulesResponse>> GetRulesAsync(
        GetMessagingModerationRulesRequest request,
        CancellationToken ct = default)
    {
        var tenant = ResolveAdminTenant(request.Metadata);
        if (!tenant.IsSuccess)
            return Result<GetMessagingModerationRulesResponse>.Failure(tenant.Message ?? "Tenant could not be resolved", tenant.StatusCode);

        var query = dataContext.Query<MessageModerationRule>()
            .Where(rule => rule.TenantId == tenant.Data!.TenantId);

        if (!request.IncludeInactive)
            query = query.Where(rule => !rule.IsDeleted && rule.IsEnabled);

        var rules = await query
            .OrderBy(rule => rule.Name)
            .ToListAsync(ct);

        return Result<GetMessagingModerationRulesResponse>.Success(new()
        {
            Items = rules.Select(ToRuleResponse).ToList()
        });
    }

    public async Task<Result<MessagingModerationRuleResponse>> CreateRuleAsync(
        CreateMessagingModerationRuleRequest request,
        CancellationToken ct = default)
    {
        var tenant = ResolveAdminTenant(request.Metadata);
        if (!tenant.IsSuccess)
            return Result<MessagingModerationRuleResponse>.Failure(tenant.Message ?? "Tenant could not be resolved", tenant.StatusCode);

        var validation = ValidateRule(request.Name, request.MatchType, request.Pattern, request.Action);
        if (validation.Count > 0)
            return Result<MessagingModerationRuleResponse>.ValidationError(validation);

        var name = request.Name.Trim();
        var duplicate = await dataContext.Query<MessageModerationRule>()
            .Where(rule => rule.TenantId == tenant.Data!.TenantId)
            .Where(rule => rule.Name == name)
            .Where(rule => !rule.IsDeleted)
            .AnyAsync(ct);
        if (duplicate)
            return Result<MessagingModerationRuleResponse>.Conflict("A moderation rule with this name already exists.");

        var rule = new MessageModerationRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Data!.TenantId,
            Name = name,
            MatchType = NormalizeMatchType(request.MatchType),
            Pattern = request.Pattern.Trim(),
            Action = NormalizeAction(request.Action),
            Description = NormalizeNullable(request.Description),
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(rule);
        var save = await dataContext.SaveChangesAsync(ct);
        if (!save.IsSuccess)
            return Result<MessagingModerationRuleResponse>.Failure(save.Message ?? "Moderation rule could not be created.", save.StatusCode);

        return Result<MessagingModerationRuleResponse>.Success(ToRuleResponse(rule), 201, "Moderation rule created.");
    }

    public async Task<Result<MessagingModerationRuleResponse>> UpdateRuleAsync(
        UpdateMessagingModerationRuleRequest request,
        CancellationToken ct = default)
    {
        var tenant = ResolveAdminTenant(request.Metadata);
        if (!tenant.IsSuccess)
            return Result<MessagingModerationRuleResponse>.Failure(tenant.Message ?? "Tenant could not be resolved", tenant.StatusCode);

        var rule = await dataContext.Query<MessageModerationRule>()
            .Where(item => item.TenantId == tenant.Data!.TenantId)
            .Where(item => item.Id == request.RuleId)
            .Where(item => !item.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (rule is null)
            return Result<MessagingModerationRuleResponse>.NotFound("Moderation rule not found.");

        var validation = ValidateRule(
            request.Name ?? rule.Name,
            request.MatchType ?? rule.MatchType,
            request.Pattern ?? rule.Pattern,
            request.Action ?? rule.Action);
        if (validation.Count > 0)
            return Result<MessagingModerationRuleResponse>.ValidationError(validation);

        if (request.Name is not null)
            rule.Name = request.Name.Trim();
        if (request.MatchType is not null)
            rule.MatchType = NormalizeMatchType(request.MatchType);
        if (request.Pattern is not null)
            rule.Pattern = request.Pattern.Trim();
        if (request.Action is not null)
            rule.Action = NormalizeAction(request.Action);
        if (request.Description is not null)
            rule.Description = NormalizeNullable(request.Description);
        if (request.IsEnabled is bool isEnabled)
            rule.IsEnabled = isEnabled;

        rule.ModifiedAt = DateTime.UtcNow;
        rule.ConcurrencyStamp = Guid.NewGuid();

        dataContext.Update(rule);
        var save = await dataContext.SaveChangesAsync(ct);
        if (!save.IsSuccess)
            return Result<MessagingModerationRuleResponse>.Failure(save.Message ?? "Moderation rule could not be updated.", save.StatusCode);

        return Result<MessagingModerationRuleResponse>.Success(ToRuleResponse(rule), "Moderation rule updated.");
    }

    public async Task<Result<CmdResponse>> DeleteRuleAsync(
        DeleteMessagingModerationRuleRequest request,
        CancellationToken ct = default)
    {
        var tenant = ResolveAdminTenant(request.Metadata);
        if (!tenant.IsSuccess)
            return Result<CmdResponse>.Failure(tenant.Message ?? "Tenant could not be resolved", tenant.StatusCode);

        var rule = await dataContext.Query<MessageModerationRule>()
            .Where(item => item.TenantId == tenant.Data!.TenantId)
            .Where(item => item.Id == request.RuleId)
            .Where(item => !item.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (rule is null)
            return Result<CmdResponse>.NotFound("Moderation rule not found.");

        rule.IsDeleted = true;
        rule.IsEnabled = false;
        rule.DeletedAt = DateTime.UtcNow;
        rule.ModifiedAt = rule.DeletedAt;
        dataContext.Update(rule);

        var save = await dataContext.SaveChangesAsync(ct);
        if (!save.IsSuccess)
            return Result<CmdResponse>.Failure(save.Message ?? "Moderation rule could not be deleted.", save.StatusCode);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Moderation rule deleted."
        });
    }

    public async Task<Result<MessagingReportWorkflowResponse>> ReviewReportAsync(
        ReviewMessageReportRequest request,
        CancellationToken ct = default)
    {
        var tenant = ResolveAdminTenant(request.Metadata);
        if (!tenant.IsSuccess)
            return Result<MessagingReportWorkflowResponse>.Failure(tenant.Message ?? "Tenant could not be resolved", tenant.StatusCode);

        var report = await dataContext.Query<MessageReport>()
            .Where(item => item.TenantId == tenant.Data!.TenantId)
            .Where(item => item.Id == request.ReportId)
            .Where(item => !item.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (report is null)
            return Result<MessagingReportWorkflowResponse>.NotFound("Message report not found.");

        var normalizedAction = NormalizeReportAction(request.Action);
        var fromStatus = report.Status;
        var toStatus = StatusForAction(normalizedAction, fromStatus);
        report.Status = toStatus;
        report.ModifiedAt = DateTime.UtcNow;
        dataContext.Update(report);

        var audit = new MessageReportAudit
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Data!.TenantId,
            ReportId = report.Id,
            Action = normalizedAction,
            ActorCredentialId = tenant.Data.CredentialId,
            AssignedCredentialId = request.AssignedCredentialId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = NormalizeNullable(request.Note),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(audit);

        var save = await dataContext.SaveChangesAsync(ct);
        if (!save.IsSuccess)
            return Result<MessagingReportWorkflowResponse>.Failure(save.Message ?? "Message report could not be updated.", save.StatusCode);

        var audits = await LoadAuditAsync(report.Id, tenant.Data!.TenantId, ct);
        return Result<MessagingReportWorkflowResponse>.Success(new()
        {
            ReportId = report.Id,
            Status = StatusText(report.Status),
            Action = normalizedAction,
            Audit = audits
        }, "Message report updated.");
    }

    private Result<MessagingTenantContext> ResolveAdminTenant(RequestMetadata? metadata)
    {
        var admin = requestContextResolver.ResolveAdmin(metadata);
        return admin.IsSuccess
            ? admin
            : Result<MessagingTenantContext>.Failure(admin.Message ?? "Messaging moderation requires an admin context", admin.StatusCode);
    }

    private async Task<List<MessagingReportAuditResponse>> LoadAuditAsync(Guid reportId, Guid tenantId, CancellationToken ct)
    {
        var rows = await dataContext.Query<MessageReportAudit>()
            .Where(audit => audit.ReportId == reportId)
            .Where(audit => audit.TenantId == tenantId)
            .Where(audit => !audit.IsDeleted && audit.IsEnabled)
            .OrderByDescending(audit => audit.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(audit => new MessagingReportAuditResponse
        {
            Id = audit.Id,
            Action = audit.Action,
            ActorCredentialId = audit.ActorCredentialId,
            AssignedCredentialId = audit.AssignedCredentialId,
            FromStatus = audit.FromStatus,
            ToStatus = audit.ToStatus,
            Note = audit.Note,
            CreatedAt = audit.CreatedAt
        }).ToList();
    }

    private static MessagingModerationRuleResponse ToRuleResponse(MessageModerationRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        MatchType = rule.MatchType,
        Pattern = rule.Pattern,
        Action = rule.Action,
        Description = rule.Description,
        IsEnabled = rule.IsEnabled,
        CreatedAt = rule.CreatedAt,
        ModifiedAt = rule.ModifiedAt
    };

    private static Dictionary<string, string[]> ValidateRule(string name, string matchType, string pattern, string action)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160)
            errors["Name"] = ["Rule name is required and must be 160 characters or less."];
        if (!IsKnownMatchType(matchType))
            errors["MatchType"] = ["Match type must be keyword or regex."];
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Trim().Length > 1000)
            errors["Pattern"] = ["Pattern is required and must be 1000 characters or less."];
        if (!IsKnownRuleAction(action))
            errors["Action"] = ["Action must be flag, auto-report, or block-before-send."];

        if (IsKnownMatchType(matchType) &&
            NormalizeMatchType(matchType) == MessageModerationRuleMatchTypes.Regex)
        {
            try { _ = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)); }
            catch (ArgumentException) { errors["Pattern"] = ["Regex pattern is malformed."]; }
        }

        return errors;
    }

    private static bool IsMatch(MessageModerationRule rule, string text)
    {
        var matchType = NormalizeMatchType(rule.MatchType);
        if (matchType == MessageModerationRuleMatchTypes.Regex)
        {
            try
            {
                return Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMatchType(string? value) =>
        string.Equals(value, MessageModerationRuleMatchTypes.Regex, StringComparison.OrdinalIgnoreCase)
            ? MessageModerationRuleMatchTypes.Regex
            : MessageModerationRuleMatchTypes.Keyword;

    private static string NormalizeAction(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            MessageModerationRuleActions.AutoReport => MessageModerationRuleActions.AutoReport,
            MessageModerationRuleActions.BlockBeforeSend => MessageModerationRuleActions.BlockBeforeSend,
            _ => MessageModerationRuleActions.Flag
        };

    private static string NormalizeReportAction(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            MessageReportAuditActions.Assigned => MessageReportAuditActions.Assigned,
            MessageReportAuditActions.Dismissed => MessageReportAuditActions.Dismissed,
            MessageReportAuditActions.Resolved => MessageReportAuditActions.Resolved,
            MessageReportAuditActions.Escalated => MessageReportAuditActions.Escalated,
            MessageReportAuditActions.NoteAdded => MessageReportAuditActions.NoteAdded,
            _ => MessageReportAuditActions.Reviewed
        };

    private static short StatusForAction(string action, short currentStatus) =>
        action switch
        {
            MessageReportAuditActions.Reviewed => MessageReportStatuses.Reviewed,
            MessageReportAuditActions.Dismissed => MessageReportStatuses.Dismissed,
            MessageReportAuditActions.Resolved => MessageReportStatuses.Resolved,
            MessageReportAuditActions.Escalated => MessageReportStatuses.Escalated,
            _ => currentStatus
        };

    private static string StatusText(short status) => status switch
    {
        MessageReportStatuses.Reviewed => "Reviewed",
        MessageReportStatuses.Dismissed => "Dismissed",
        MessageReportStatuses.Resolved => "Resolved",
        MessageReportStatuses.Escalated => "Escalated",
        _ => "Open"
    };

    private static bool IsKnownMatchType(string? value) =>
        string.Equals(value, MessageModerationRuleMatchTypes.Keyword, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, MessageModerationRuleMatchTypes.Regex, StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownRuleAction(string? value) =>
        string.Equals(value, MessageModerationRuleActions.Flag, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, MessageModerationRuleActions.AutoReport, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, MessageModerationRuleActions.BlockBeforeSend, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
