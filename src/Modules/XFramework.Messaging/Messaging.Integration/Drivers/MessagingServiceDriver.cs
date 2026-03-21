using Messaging.Domain.Shared.Contracts.Requests.Create;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Messaging.Integration.Drivers;

public interface IMessagingServiceWrapper : IServiceWrapper
{
    Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request);
    Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request);
}

public sealed record MessagingServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), IMessagingServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "Messaging".ToSha256();
    }

    public async Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request)
    {
        return await SendVoidAsync(request);
    }
}

public static class MessagingServiceWrapperExtensions
{
    public static void AddMessagingWrapperServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
    }
}
