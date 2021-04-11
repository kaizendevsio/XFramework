using System;
using System.Threading.Tasks;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Integration.Interfaces
{
    public interface IRecordsWrapper
    {
        public Task<Guid> NewLog(string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information);
        public Task<Guid> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid);
        public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information);

    }
}