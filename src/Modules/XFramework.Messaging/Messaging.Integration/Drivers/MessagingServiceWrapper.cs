using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using XFramework.Domain.Shared.BusinessObjects;

namespace Messaging.Integration.Drivers;

public partial interface IMessagingServiceWrapper
{
    public Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request);
    public Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request);
    public Task<CmdResponse> ConfirmMessageSent(UpdateMessageDirectRequest request);

}

public partial record MessagingServiceWrapper
{
    public Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ConfirmMessageSent(UpdateMessageDirectRequest request)
    {
        return SendVoidAsync(request);
    }
}   