using XFramework.Domain.Shared.Enums;

namespace XFramework.Integration.Abstractions.Wrappers;

public interface ILoggerWrapper : IXFrameworkService
{
    public Task<Guid?> NewLog(string name, string message, string initiator, RequestMetadata requestMetadata, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information);
    public Task<Guid?> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid);
    public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information);

}