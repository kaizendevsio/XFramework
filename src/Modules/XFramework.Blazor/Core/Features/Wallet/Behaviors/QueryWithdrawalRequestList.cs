using Microsoft.Extensions.Logging;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record QueryWithdrawalRequestList() : StateAction<PaginatedResult<WithdrawalRequest>?>
    {
        public Guid? CredentialId { get; set; }
    };

    protected class QueryWithdrawalRequestListHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        IDialogService dialogService,
        HandlerServices handlerServices,
        ILogger<QueryWithdrawalRequestListHandler> logger,
        IStore store
    ) : StateActionHandler<QueryWithdrawalRequestList, PaginatedResult<WithdrawalRequest>?>(handlerServices, store)
    {
        public override async Task<PaginatedResult<WithdrawalRequest>?> Handle(QueryWithdrawalRequestList action, CancellationToken aCancellationToken)
        {
            var credentialId = action.CredentialId ?? SessionState.Credential?.Id;
            if (credentialId is null)
            {
                await HandleFailure(action, "Credential id is required to create withdrawal request");
                return null;
            }
            
            var response = await walletsServiceWrapper.WithdrawalRequest.GetList(
                pageSize: 500,
                pageNumber: 1,
                filter:[
                    new()
                    {
                        PropertyName = nameof(WithdrawalRequest.CredentialId),
                        Operation = QueryFilterOperation.Equal,
                        Value = credentialId.Value
                    }
                ],
                includeNavigations: true,
                includes:[
                    $"{nameof(WithdrawalRequest.Wallet)}.{nameof(Domain.Shared.Contracts.Wallet.WalletType)}",
                ]
            );
            
            if (await HandleFailure(response, action)) return null;
            
            return response.Response;
        }
    }
}