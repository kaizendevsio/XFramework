using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Integration.Drivers;

public record DriverBase(IMessageBusWrapper? MessageBusDriver, IConfiguration? Configuration)
{
    public DriverBase() : this(null, null)
    {
    }

    public bool IsConnected => MessageBusDriver?.IsConnected ?? false;

    public virtual void Initialize()
    {
        throw new NotSupportedException(
            $"{GetType().Name} does not define a target Bolt client. " +
            $"Set {nameof(TargetClient)} or override {nameof(Initialize)} before sending requests.");
    }

    public string? TargetClient { get; set; }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request)
        where TRequest : class, IHasRequestServer
    {
        return await GetMessageBusDriver().SendVoidAsync(request, GetTargetClient());
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request)
        where TRequest : class, IHasRequestServer
    {
        return await GetMessageBusDriver().SendVoidAsync<TRequest, TResponse>(request, GetTargetClient());
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request)
        where TRequest : class, IHasRequestServer
    {
        return await GetMessageBusDriver().SendAsync<TRequest, TResponse>(request, GetTargetClient());
    }

    private IMessageBusWrapper GetMessageBusDriver() =>
        MessageBusDriver ?? throw new InvalidOperationException(
            $"{GetType().Name} cannot send requests without an {nameof(IMessageBusWrapper)}.");

    private string GetTargetClient()
    {
        if (string.IsNullOrWhiteSpace(TargetClient))
            Initialize();

        if (string.IsNullOrWhiteSpace(TargetClient))
            throw new InvalidOperationException(
                $"{GetType().Name} did not configure {nameof(TargetClient)} during initialization.");

        return TargetClient;
    }
}
