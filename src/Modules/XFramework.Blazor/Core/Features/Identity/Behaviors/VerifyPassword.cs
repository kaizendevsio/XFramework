using IdentityServer.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record VerifyPassword(string Password, Guid? CredentialId = null) : StateAction<CmdResponse>;
    
    protected class VerifyPasswordHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<VerifyPassword, CmdResponse>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        
        public override async Task<CmdResponse> Handle(VerifyPassword request, CancellationToken cancellationToken)
        {
            await ReportBusy();
            
            var response = await identityServerServiceWrapper.VerifyPassword(new()
            {
                CredentialId = request.CredentialId ?? CurrentState.Credential.Id,
                Password = request.Password
            });
            if (await HandleFailure(response, request, true)) return response;
            
            await HandleSuccess(response, request, true);
            await ReportTaskCompleted();
            
            return response;
        }
    }
}