using IdentityServer.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record UpdatePassword : StateAction
    {
        public string? Password { get; set; }
    }

    protected class UpdatePasswordHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<UpdatePassword>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        

        public override async Task Handle(UpdatePassword request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(CurrentState.Credential);
            
            var result = await identityServerServiceWrapper.ChangePassword(new()
            {
                CreadentialId = CurrentState.Credential.Id,
                NewPassword = request.Password,
                RequireVerificationId = false
            });
                    
            // Handle if the response is invalid or error
            if (await HandleFailure(result, request, false, "There was an error changing your password"))
            {
                return;
            }
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(result, request, true, "Password changed successfully");
        }
    }
}

