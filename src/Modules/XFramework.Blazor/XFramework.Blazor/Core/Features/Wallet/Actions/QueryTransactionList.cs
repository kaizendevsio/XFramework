using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record QueryTransactionList(Guid WalletId) : StateAction<PaginatedResult<WalletTransaction>?>;

    protected class QueryTransactionListHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<QueryTransactionList, PaginatedResult<WalletTransaction>?>(handlerServices, store)
    {
        public override async Task<PaginatedResult<WalletTransaction>?> Handle(QueryTransactionList action, CancellationToken aCancellationToken)
        {
            var walletTransactionResponse = await walletsServiceWrapper.WalletTransaction.GetList(
                pageSize: 500,
                pageNumber: 1,
                filter:[
                    new()
                    {
                        PropertyName = nameof(WalletTransaction.WalletId),
                        Operation = QueryFilterOperation.Equal,
                        Value = action.WalletId
                    }
                ],
                includeNavigations: true,
                includes:[
                    $"{nameof(WalletTransaction.Wallet)}.{nameof(Domain.Shared.Contracts.Wallet.WalletType)}",
                ]
            );
            
            if (await HandleFailure(walletTransactionResponse, action)) return null;
            
            return walletTransactionResponse.Response;
        }
    }
}