using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Integration.Drivers;

public class RecordsDriver : ILoggerWrapper
{
    private IMessageBusWrapper MessageBusWrapper { get; }

    public RecordsDriver(IMessageBusWrapper messageBusWrapper)
    {
        MessageBusWrapper = messageBusWrapper;
    }
        
    public Task<Guid?> NewLog(string title, string message, Guid? guid = null, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
    {
        guid ??= Guid.NewGuid();

        return Task.FromResult(guid);
    }

    public Task<Guid?> NewLog(string name, string message, string initiator, RequestMetadata requestMetadata, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
    {
        return Task.FromResult<Guid?>(Guid.NewGuid());
    }

    public Task<Guid?> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid)
    {
        return Task.FromResult<Guid?>(cuid);
    }

    public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
    {
        return Task.CompletedTask;
    }
}
