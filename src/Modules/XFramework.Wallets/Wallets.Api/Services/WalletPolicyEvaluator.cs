using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed class WalletPolicyEvaluator : IWalletPolicyEvaluator
{
    public Task<Result<WalletPolicyEvaluationResult>> EvaluateAsync(
        WalletPolicyEvaluationContext context,
        CancellationToken ct = default)
    {
        foreach (var posting in context.Request.Postings.Where(static p => p.WalletId.HasValue))
        {
            var walletId = posting.WalletId!.Value;
            if (!context.Wallets.TryGetValue(walletId, out var wallet))
            {
                return Task.FromResult(Result<WalletPolicyEvaluationResult>.NotFound("Wallet not found"));
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
                return Task.FromResult(Result<WalletPolicyEvaluationResult>.Forbidden(statusFailure));
            }

            if (posting.Direction is not WalletLedgerDirection.Debit)
            {
                continue;
            }

            if (posting.BalanceBucket is WalletBalanceBucket.Available &&
                wallet.AvailableBalance < posting.Amount)
            {
                return Task.FromResult(Result<WalletPolicyEvaluationResult>.Failure("Insufficient funds", 400));
            }

            if (posting.BalanceBucket is WalletBalanceBucket.DebitHold &&
                wallet.AvailableBalance < posting.Amount)
            {
                return Task.FromResult(Result<WalletPolicyEvaluationResult>.Failure("Insufficient funds for hold", 400));
            }
        }

        return Task.FromResult(Result<WalletPolicyEvaluationResult>.Success(
            WalletPolicyEvaluationResult.Approved()));
    }
}
