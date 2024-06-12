using Microsoft.Extensions.Logging;
using Wallets.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record CreateWithdrawalRequest : StateAction<CmdResponse>
    {
        public decimal? Amount { get; set; }
        public decimal? Fee { get; set; }
        public Guid WalletTypeId { get; set; }
        public Guid? CredentialId { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    protected class CreateWithdrawalRequestHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        IDialogService dialogService,
        HandlerServices handlerServices,
        ILogger<CreateWithdrawalRequestHandler> logger,
        IStore store
    ) : StateActionHandler<CreateWithdrawalRequest, CmdResponse>(handlerServices, store)
    {

        public override async Task<CmdResponse> Handle(CreateWithdrawalRequest action, CancellationToken aCancellationToken)
        {
            var credentialId = action.CredentialId ?? SessionState.Credential?.Id;
            if (credentialId is null)
            {
                await HandleFailure(action, "Credential id is required to create withdrawal request");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }
            
            if (action.WalletTypeId == Guid.Empty)
            {
                await HandleFailure(action, "Target wallet type is required to create withdrawal request");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }

            var wallet = await walletsServiceWrapper.Wallet.GetList(
                pageSize: 2,
                pageNumber: 1,
                filter:
                [
                    new()
                    {
                        PropertyName = nameof(Domain.Shared.Contracts.Wallet.WalletTypeId),
                        Operation = QueryFilterOperation.Equal,
                        Value = action.WalletTypeId,
                    },
                    new()
                    {
                        PropertyName = nameof(Domain.Shared.Contracts.Wallet.CredentialId),
                        Operation = QueryFilterOperation.Equal,
                        Value = credentialId
                    }
                ]
            );
            
            if (await HandleFailure(wallet, action)) return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest
            };

            if (wallet.Response?.TotalItems == 0)
            {
                await HandleFailure(action, "Wallet not found");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }

            if (wallet.Response?.TotalItems > 1)
            {
                await HandleFailure(action, "Ambiguous wallet found. Please call technical support for help");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.BadRequest
                };
            }
            
            logger.LogInformation("Creating withdrawal request for credentialId {CredentialId}", credentialId);
            ReportBusy("Creating withdrawal request...");

            var withdrawalRequest = await walletsServiceWrapper.WithdrawalRequest.Create(new()
            {
                CredentialId = credentialId.Value,
                Fee = action.Fee,
                Amount = action.Amount!.Value,
                WithdrawalStatus = TransactionStatus.Pending,
                Remarks = "Withdrawal Request",
                ReferenceNumber = action.ReferenceNumber,
                WalletId = wallet.Response!.Items.First().Id,
            });
            
            if (withdrawalRequest.IsSuccess is false)
            {
                await HandleFailure(action, "Failed to create withdrawal request");
                return withdrawalRequest.Adapt<CmdResponse>();
            }

            await walletsServiceWrapper.DecrementWallet(new()
            {
                CredentialId = credentialId.Value,
                WalletId = wallet.Response!.Items.First().Id,
                Amount = action.Amount!.Value,
                Fee = action.Fee ?? 0,
                Remarks = "Withdrawal Request",
                OnHold = true,
                ReferenceNumber = action.ReferenceNumber,
                CurrencyId = Constants.Currency.Php,
            });
            
            await HandleSuccess(action,"Withdrawal request created successfully");
            return withdrawalRequest.Adapt<CmdResponse>();
        }
    }
}