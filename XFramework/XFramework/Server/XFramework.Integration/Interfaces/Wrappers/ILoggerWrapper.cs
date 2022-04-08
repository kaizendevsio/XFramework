using XFramework.Domain.Generic.Enums;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers;

public interface ILoggerWrapper : IXFrameworkService
{
    public Task<Guid?> NewLog(string name, string message, string initiator, RequestServerBO requestServer, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information);
    public Task<Guid?> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid);
    public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information);

}