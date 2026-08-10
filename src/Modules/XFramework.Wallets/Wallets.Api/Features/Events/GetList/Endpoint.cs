using Wallets.Api.Events;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Events.GetList;

public static class GetWalletEventsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.View])]
    [MapGet("/api/wallets/events", Tags = ["Wallet Events"],
        Summary = "Get recent wallet events",
        Description = "Retrieves recent wallet events from the in-memory buffer. Supports filtering by WalletId, CredentialId, and EventType with pagination.",
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.View],
        ExcludeFromOpenApi = true)]
    public static Task<Result<List<WalletEventResponse>>> Handle(
        GetWalletEventsRequest request,
        IWalletEventPublisher eventPublisher,
        IWalletRequestContextResolver contextResolver,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request, request.CredentialId);
        if (!contextResult.IsSuccess)
        {
            return Task.FromResult(Result<List<WalletEventResponse>>.Failure(
                contextResult.Message!,
                contextResult.StatusCode));
        }

        var context = contextResult.Data!;
        var credentialId = context.IsPrivilegedActor
            ? request.CredentialId
            : context.ActorCredentialId;
        var events = eventPublisher.GetRecentEvents(
            context.TenantId,
            request.WalletId,
            credentialId,
            request.EventType,
            request.PageIndex,
            request.PageSize);

        var responses = events.Select(MapToResponse).ToList();

        return Task.FromResult(Result<List<WalletEventResponse>>.Success(responses));
    }

    private static WalletEventResponse MapToResponse(WalletEvent e)
    {
        var response = new WalletEventResponse
        {
            EventId = e.EventId,
            OccurredAt = e.OccurredAt,
            EventType = e.EventType,
            WalletId = e.WalletId,
            CredentialId = e.CredentialId,
            TenantId = e.TenantId
        };

        switch (e)
        {
            case TransactionCompletedEvent tc:
                response.Amount = tc.Amount;
                response.TransactionType = tc.TransactionType;
                response.ReferenceNumber = tc.ReferenceNumber;
                response.RunningBalance = tc.RunningBalance;
                break;
            case WalletFrozenEvent wf:
                response.Reason = wf.Reason;
                break;
            case LargeTransactionEvent lt:
                response.Amount = lt.Amount;
                response.TransactionType = lt.TransactionType;
                response.Threshold = lt.Threshold;
                break;
            case TransactionReversedEvent tr:
                response.Amount = tr.Amount;
                response.OriginalTransactionId = tr.OriginalTransactionId;
                response.ReversalTransactionId = tr.ReversalTransactionId;
                break;
        }

        return response;
    }
}
