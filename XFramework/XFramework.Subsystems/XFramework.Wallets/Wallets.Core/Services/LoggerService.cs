using XFramework.Integration.Interfaces.Wrappers;

namespace Wallets.Core.Services;

public class LoggerService : ILoggerWrapper
{
    private readonly IDataLayer _dataLayer;

    public LoggerService(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<Guid?> NewLog(string name, string message, string initiator, RequestServerBO requestServer, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
    {
        var entity = await _dataLayer.Applications
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{requestServer.ApplicationId}");
        if (entity is null)
        {
            throw new ArgumentException($"Application with Guid '{requestServer.ApplicationId}' does not exist in any tenants");
        }
        var log = new Log()
        {
            ApplicationId = entity.Id,
            Initiator = initiator,
            Severity = (short) priorityType,
            Message = message,
            Name = name,
            Type = (short?) logType,
            Guid = requestServer.RequestId.ToString()
        };

        _dataLayer.Logs.Add(log);
        await _dataLayer.SaveChangesAsync();

        return requestServer.RequestId;
    }

    public Task<Guid?> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid)
    {
        throw new NotImplementedException();
    }

    public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
    {
        throw new NotImplementedException();
    }
}