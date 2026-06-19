using XFramework.Core.Patterns;
using System.Text.Json;

namespace Wallets.Api.Services;

public sealed class WalletPolicyEvaluator(DbContext dbContext) : IWalletPolicyEvaluator
{
    public Task<Result<WalletPolicyEvaluationResult>> EvaluateAsync(
        WalletPolicyEvaluationContext context,
        CancellationToken ct = default)
    {
        return EvaluateCoreAsync(context, ct);
    }

    private async Task<Result<WalletPolicyEvaluationResult>> EvaluateCoreAsync(
        WalletPolicyEvaluationContext context,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var walletPostings = context.Request.Postings
            .Where(static p => p.WalletId.HasValue)
            .ToList();
        var walletTypeIds = walletPostings
            .Select(static p => p.WalletTypeId)
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .Distinct()
            .ToList();
        var currencyIds = walletPostings
            .Select(static p => p.CurrencyId)
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .Distinct()
            .ToList();

        var rules = await dbContext.Set<WalletPolicyRule>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == context.Request.TenantId &&
                !x.IsDeleted &&
                x.IsEnabled &&
                (x.OperationType == null || x.OperationType == context.Request.OperationType) &&
                (x.WalletTypeId == null || walletTypeIds.Contains(x.WalletTypeId.Value)) &&
                (x.CurrencyId == null || currencyIds.Contains(x.CurrencyId.Value)) &&
                x.EffectiveAt <= now &&
                (x.ExpiresAt == null || x.ExpiresAt > now))
            .ToListAsync(ct);

        var requiresApproval = false;
        var decisions = new List<object>();

        foreach (var posting in walletPostings)
        {
            var walletId = posting.WalletId!.Value;
            if (!context.Wallets.TryGetValue(walletId, out var wallet))
            {
                return Result<WalletPolicyEvaluationResult>.NotFound("Wallet not found");
            }

            var statusFailure = wallet.Status switch
            {
                WalletStatus.Frozen => "Wallet is frozen. No operations allowed.",
                WalletStatus.Suspended => "Wallet is suspended. No operations allowed.",
                WalletStatus.Closed => "Wallet is closed. No operations allowed.",
                _ => null
            };

            if (statusFailure is not null)
            {
                return Result<WalletPolicyEvaluationResult>.Forbidden(statusFailure);
            }

            var matchingRules = rules
                .Where(rule =>
                    (rule.WalletTypeId == null || rule.WalletTypeId == wallet.WalletTypeId) &&
                    (rule.CurrencyId == null || rule.CurrencyId == posting.CurrencyId) &&
                    (rule.RequiredWalletStatus == null || rule.RequiredWalletStatus == wallet.Status))
                .ToList();

            if (matchingRules.Any(static rule => rule.DenyWhenMatched))
            {
                return Result<WalletPolicyEvaluationResult>.Forbidden("Wallet policy rejected the operation");
            }

            foreach (var rule in matchingRules)
            {
                if (rule.MaxSingleTransactionAmount.HasValue && posting.Amount > rule.MaxSingleTransactionAmount.Value)
                {
                    return Result<WalletPolicyEvaluationResult>.Failure(
                        $"Amount exceeds policy limit {rule.MaxSingleTransactionAmount.Value}",
                        400);
                }

                if (rule.ApprovalThreshold.HasValue && posting.Amount >= rule.ApprovalThreshold.Value)
                {
                    requiresApproval = true;
                }
            }

            if (posting.Direction is not WalletLedgerDirection.Debit)
            {
                continue;
            }

            if (posting.BalanceBucket is WalletBalanceBucket.Available &&
                wallet.AvailableBalance < posting.Amount)
            {
                return Result<WalletPolicyEvaluationResult>.Failure("Insufficient funds", 400);
            }

            if (posting.BalanceBucket is WalletBalanceBucket.DebitHold &&
                wallet.AvailableBalance < posting.Amount)
            {
                return Result<WalletPolicyEvaluationResult>.Failure("Insufficient funds for hold", 400);
            }

            var velocityResult = await CheckVelocityAsync(
                context.Request.TenantId,
                walletId,
                posting.Amount,
                matchingRules,
                now,
                ct);

            if (!velocityResult.IsSuccess)
            {
                return velocityResult;
            }

            decisions.Add(new
            {
                walletId,
                posting.Amount,
                posting.Direction,
                posting.BalanceBucket,
                ruleCount = matchingRules.Count,
                requiresApproval
            });
        }

        var decisionJson = JsonSerializer.Serialize(new
        {
            approved = true,
            requiresApproval,
            context.Request.OperationType,
            decisions
        });

        return Result<WalletPolicyEvaluationResult>.Success(
            new WalletPolicyEvaluationResult(
                true,
                requiresApproval ? "approval_required" : "approved",
                RequiresApproval: requiresApproval,
                DecisionJson: decisionJson));
    }

    private async Task<Result<WalletPolicyEvaluationResult>> CheckVelocityAsync(
        Guid tenantId,
        Guid walletId,
        decimal requestedAmount,
        IReadOnlyList<WalletPolicyRule> rules,
        DateTime now,
        CancellationToken ct)
    {
        var dailyLimit = rules
            .Where(static rule => rule.DailyVelocityLimit.HasValue)
            .Select(static rule => rule.DailyVelocityLimit!.Value)
            .DefaultIfEmpty()
            .Min();
        var monthlyLimit = rules
            .Where(static rule => rule.MonthlyVelocityLimit.HasValue)
            .Select(static rule => rule.MonthlyVelocityLimit!.Value)
            .DefaultIfEmpty()
            .Min();

        if (dailyLimit <= 0 && monthlyLimit <= 0)
        {
            return Result<WalletPolicyEvaluationResult>.Success(WalletPolicyEvaluationResult.Approved());
        }

        var dayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (dailyLimit > 0)
        {
            var dailyTotal = await SumWalletLedgerAmountAsync(tenantId, walletId, dayStart, now, ct);
            if (dailyTotal + requestedAmount > dailyLimit)
            {
                return Result<WalletPolicyEvaluationResult>.Failure("Daily wallet velocity limit exceeded", 400);
            }
        }

        if (monthlyLimit > 0)
        {
            var monthlyTotal = await SumWalletLedgerAmountAsync(tenantId, walletId, monthStart, now, ct);
            if (monthlyTotal + requestedAmount > monthlyLimit)
            {
                return Result<WalletPolicyEvaluationResult>.Failure("Monthly wallet velocity limit exceeded", 400);
            }
        }

        return Result<WalletPolicyEvaluationResult>.Success(WalletPolicyEvaluationResult.Approved());
    }

    private Task<decimal> SumWalletLedgerAmountAsync(
        Guid tenantId,
        Guid walletId,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        return dbContext.Set<WalletLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.WalletId == walletId &&
                x.Direction == WalletLedgerDirection.Debit &&
                x.BalanceBucket == WalletBalanceBucket.Available &&
                x.CreatedAt >= from &&
                x.CreatedAt <= to)
            .SumAsync(x => x.Amount, ct);
    }
}
