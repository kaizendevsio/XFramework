using Wallets.Integration.Drivers;
using XFramework.Integration.Abstractions;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public record CreateWallet : StateAction
    {
        public bool ReloadWalletList { get; set; }
        public Guid WalletTypeId { get; set; }
        public decimal Balance { get; set; } = 0;
        public Guid CredentialId { get; set; }
        public bool Silent { get; set; }
    }
    
    protected class CreateWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        IHelperService helperService,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<CreateWallet>(handlerServices, store)
    {
        public SessionState WalletState => Store.GetState<SessionState>();
        
        public override async Task Handle(CreateWallet action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            if (!action.Silent)
            {
                ReportBusy("Creating Wallet..", true);
            }

            // Check if CredentialGuid is provided
            if (SessionState.State is not CurrentSessionState.Active && action.CredentialId == Guid.Empty)
            {
                SweetAlertService.FireAsync("Error", "Could not create wallet, credentials not provided");
                return;
            }
            
            // Map view model to request object
            var request = action.Adapt<Domain.Generic.Contracts.Wallet>();
            request.CredentialId = action.CredentialId;
            
            // Get the wallet type
            var walletType = await walletsServiceWrapper.WalletType.Get(id: action.WalletTypeId);
            if (await HandleFailure(walletType, action, true, "Could not create wallet, wallet type not found")) return;
            
            // Set the wallet type
            request.WalletTypeId = walletType.Response.Id;
            request.BondBalanceRule = walletType.Response.BondBalanceRule;
            request.MaintainingBalanceRule = walletType.Response.MaintainingBalanceRule;
            request.MinTransferRule = walletType.Response.MinTransferRule;
            request.MaxTransferRule = walletType.Response.MaxTransferRule;
            request.AccountNumber = $"{helperService.GenerateRandomNumber(1000_0000_0000, 9999_9999_9999)}";
            
            checkAccountNumber:
            // check if the account number is unique
            var accountNumberExists = await walletsServiceWrapper.Wallet.GetList(
                pageSize:2,
                pageNumber:1,
                filter: new List<QueryFilter>
                {
                    new()
                    {
                        PropertyName = nameof(Domain.Generic.Contracts.Wallet.AccountNumber),
                        Operation = QueryFilterOperation.Equal,
                        Value = request.AccountNumber
                    }
                }
            );
            
            if (await HandleFailure(accountNumberExists, action, true, "Could not check account number uniqueness")) return;
            if (accountNumberExists.Response.TotalItems > 0)
            {
                request.AccountNumber = $"{helperService.GenerateRandomNumber(1000_0000_0000, 9999_9999_9999)}";
                goto checkAccountNumber;
            }
            
            // Send the request
            var response = await walletsServiceWrapper.Wallet.Create(request);
            
            if (!action.Silent)
            {
                // Handle if the response is invalid or error
                if (await HandleFailure(response, action, true, "There was an error while trying to create your wallet. Please check your internet connection and try again")) return;

                // If Success URL property is provided, navigate to the given URL
                await HandleSuccess(response, action, true);
            }

            // Set State And Update UI
            if (action.ReloadWalletList)
            {
                await Mediator.Send(new LoadWalletList());
            }
            
            // Inform UI About Not Busy State
            if (!action.Silent)
            {
                ReportBusy("Done", false);
            }

            return;

        }
    }
}