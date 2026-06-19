using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public interface IWalletReconciliationService
{
    Task<Result<WalletReconciliationRunResponse>> RunAsync(RunWalletReconciliationRequest request, CancellationToken ct = default);
    Task<Result<WalletReconciliationItemResponse>> MarkReconciledAsync(MarkWalletReconciliationItemRequest request, CancellationToken ct = default);
}
