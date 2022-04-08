using Microsoft.Extensions.Logging;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.Interfaces;

public interface IXLogger
{
    public Task Log(string name, string message, object service, Guid? guid = null, LogLevel logLevel = LogLevel.Information, LogType logType = LogType.ApplicationServiceLog);
}