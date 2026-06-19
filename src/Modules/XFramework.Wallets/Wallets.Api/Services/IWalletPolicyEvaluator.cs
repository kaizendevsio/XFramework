using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed record WalletPolicyEvaluationResult(
    bool IsApproved,
    string Decision,
    string? Message = null)
{
    public static WalletPolicyEvaluationResult Approved(string decision = "approved") => new(true, decision);
    public static WalletPolicyEvaluationResult Rejected(string decision, string message) => new(false, decision, message);
}

public sealed record WalletPolicyEvaluationContext(
    WalletLedgerExecutionRequest Request,
    IReadOnlyDictionary<Guid, Wallet> Wallets);

public interface IWalletPolicyEvaluator
{
    Task<Result<WalletPolicyEvaluationResult>> EvaluateAsync(
        WalletPolicyEvaluationContext context,
        CancellationToken ct = default);
}
