using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public interface IWalletPolicyAdminService
{
    Task<Result<WalletPolicyRuleResponse>> UpsertPolicyRuleAsync(
        UpsertWalletPolicyRuleRequest request,
        CancellationToken ct = default);

    Task<Result<WalletFeeScheduleResponse>> UpsertFeeScheduleAsync(
        UpsertWalletFeeScheduleRequest request,
        CancellationToken ct = default);
}

public sealed class WalletPolicyAdminService(
    DbContext dbContext,
    IWalletRequestContextResolver contextResolver) : IWalletPolicyAdminService
{
    public async Task<Result<WalletPolicyRuleResponse>> UpsertPolicyRuleAsync(
        UpsertWalletPolicyRuleRequest request,
        CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return Result<WalletPolicyRuleResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<WalletPolicyRuleResponse>.Failure("Policy rule name is required", 400);
        }

        if (request.ExpiresAt.HasValue &&
            request.EffectiveAt.HasValue &&
            request.ExpiresAt.Value <= request.EffectiveAt.Value)
        {
            return Result<WalletPolicyRuleResponse>.Failure("Policy rule expiry must be after effective date", 400);
        }

        var tenantId = contextResult.Data!.TenantId;
        var rule = request.Id.HasValue
            ? await dbContext.Set<WalletPolicyRule>()
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value && x.TenantId == tenantId && !x.IsDeleted, ct)
            : null;

        var created = rule is null;
        rule ??= new WalletPolicyRule
        {
            Id = request.Id.GetValueOrDefault(Guid.NewGuid()),
            TenantId = tenantId
        };

        rule.Name = request.Name.Trim();
        rule.OperationType = request.OperationType;
        rule.WalletTypeId = request.WalletTypeId;
        rule.CurrencyId = request.CurrencyId;
        rule.RequiredWalletStatus = request.RequiredWalletStatus;
        rule.MaxSingleTransactionAmount = request.MaxSingleTransactionAmount;
        rule.DailyVelocityLimit = request.DailyVelocityLimit;
        rule.MonthlyVelocityLimit = request.MonthlyVelocityLimit;
        rule.ApprovalThreshold = request.ApprovalThreshold;
        rule.DenyWhenMatched = request.DenyWhenMatched;
        rule.RiskTier = request.RiskTier;
        rule.DecisionCode = request.DecisionCode;
        rule.EffectiveAt = request.EffectiveAt ?? rule.EffectiveAt;
        rule.ExpiresAt = request.ExpiresAt;
        rule.IsEnabled = request.IsEnabled;

        if (created)
        {
            dbContext.Set<WalletPolicyRule>().Add(rule);
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<WalletPolicyRuleResponse>.Success(ToPolicyRuleResponse(rule, created ? "Policy rule created" : "Policy rule updated"));
    }

    public async Task<Result<WalletFeeScheduleResponse>> UpsertFeeScheduleAsync(
        UpsertWalletFeeScheduleRequest request,
        CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return Result<WalletFeeScheduleResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<WalletFeeScheduleResponse>.Failure("Fee schedule name is required", 400);
        }

        if (request.FixedFee < 0 || request.PercentageFee < 0)
        {
            return Result<WalletFeeScheduleResponse>.Failure("Fee values cannot be negative", 400);
        }

        if (request.MaximumFee.HasValue &&
            request.MinimumFee.HasValue &&
            request.MaximumFee.Value < request.MinimumFee.Value)
        {
            return Result<WalletFeeScheduleResponse>.Failure("Maximum fee cannot be lower than minimum fee", 400);
        }

        if (request.ExpiresAt.HasValue &&
            request.EffectiveAt.HasValue &&
            request.ExpiresAt.Value <= request.EffectiveAt.Value)
        {
            return Result<WalletFeeScheduleResponse>.Failure("Fee schedule expiry must be after effective date", 400);
        }

        var tenantId = contextResult.Data!.TenantId;
        var schedule = request.Id.HasValue
            ? await dbContext.Set<WalletFeeSchedule>()
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value && x.TenantId == tenantId && !x.IsDeleted, ct)
            : null;

        var created = schedule is null;
        schedule ??= new WalletFeeSchedule
        {
            Id = request.Id.GetValueOrDefault(Guid.NewGuid()),
            TenantId = tenantId
        };

        schedule.Name = request.Name.Trim();
        schedule.OperationType = request.OperationType;
        schedule.WalletTypeId = request.WalletTypeId;
        schedule.CurrencyId = request.CurrencyId;
        schedule.FixedFee = request.FixedFee;
        schedule.PercentageFee = request.PercentageFee;
        schedule.MinimumFee = request.MinimumFee;
        schedule.MaximumFee = request.MaximumFee;
        schedule.AllowRequestedFeeOverride = request.AllowRequestedFeeOverride;
        schedule.EffectiveAt = request.EffectiveAt ?? schedule.EffectiveAt;
        schedule.ExpiresAt = request.ExpiresAt;
        schedule.IsEnabled = request.IsEnabled;

        if (created)
        {
            dbContext.Set<WalletFeeSchedule>().Add(schedule);
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<WalletFeeScheduleResponse>.Success(ToFeeScheduleResponse(schedule, created ? "Fee schedule created" : "Fee schedule updated"));
    }

    private static WalletPolicyRuleResponse ToPolicyRuleResponse(WalletPolicyRule rule, string message) =>
        new()
        {
            Id = rule.Id,
            Name = rule.Name,
            OperationType = rule.OperationType,
            WalletTypeId = rule.WalletTypeId,
            MaxSingleTransactionAmount = rule.MaxSingleTransactionAmount,
            DailyVelocityLimit = rule.DailyVelocityLimit,
            MonthlyVelocityLimit = rule.MonthlyVelocityLimit,
            ApprovalThreshold = rule.ApprovalThreshold,
            DenyWhenMatched = rule.DenyWhenMatched,
            IsEnabled = rule.IsEnabled,
            Message = message
        };

    private static WalletFeeScheduleResponse ToFeeScheduleResponse(WalletFeeSchedule schedule, string message) =>
        new()
        {
            Id = schedule.Id,
            Name = schedule.Name,
            OperationType = schedule.OperationType,
            WalletTypeId = schedule.WalletTypeId,
            FixedFee = schedule.FixedFee,
            PercentageFee = schedule.PercentageFee,
            MinimumFee = schedule.MinimumFee,
            MaximumFee = schedule.MaximumFee,
            AllowRequestedFeeOverride = schedule.AllowRequestedFeeOverride,
            IsEnabled = schedule.IsEnabled,
            Message = message
        };
}
