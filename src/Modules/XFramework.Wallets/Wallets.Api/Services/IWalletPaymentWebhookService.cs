using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public interface IWalletPaymentWebhookService
{
    Task<Result<WalletWebhookIngestResponse>> IngestAsync(IngestWalletPaymentWebhookRequest request, CancellationToken ct = default);
}
