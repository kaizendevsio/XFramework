using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Batch;

public static class BatchIncrementWalletBoltEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static async Task<Result<BatchOperationResult>> Handle(
        BatchIncrementWalletRequest request,
        IBatchWalletService batchService,
        IWalletRequestContextResolver contextResolver,
        CancellationToken ct)
    {
        var context = contextResolver.Resolve(request);
        return context.IsSuccess
            ? await batchService.BatchIncrementAsync(request.Requests, context.Data!, request.AllowPartialSuccess, request.IdempotencyKey, ct)
            : Result<BatchOperationResult>.Failure(context.Message!, context.StatusCode);
    }
}

public static class BatchDecrementWalletBoltEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static async Task<Result<BatchOperationResult>> Handle(
        BatchDecrementWalletRequest request,
        IBatchWalletService batchService,
        IWalletRequestContextResolver contextResolver,
        CancellationToken ct)
    {
        var context = contextResolver.Resolve(request);
        return context.IsSuccess
            ? await batchService.BatchDecrementAsync(request.Requests, context.Data!, request.AllowPartialSuccess, request.IdempotencyKey, ct)
            : Result<BatchOperationResult>.Failure(context.Message!, context.StatusCode);
    }
}

public static class BatchTransferWalletBoltEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static async Task<Result<BatchOperationResult>> Handle(
        BatchTransferWalletRequest request,
        IBatchWalletService batchService,
        IWalletRequestContextResolver contextResolver,
        CancellationToken ct)
    {
        var context = contextResolver.Resolve(request);
        return context.IsSuccess
            ? await batchService.BatchTransferAsync(request.Requests, context.Data!, request.AllowPartialSuccess, request.IdempotencyKey, ct)
            : Result<BatchOperationResult>.Failure(context.Message!, context.StatusCode);
    }
}
