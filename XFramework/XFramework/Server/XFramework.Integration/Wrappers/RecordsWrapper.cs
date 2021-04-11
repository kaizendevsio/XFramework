using System;
using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;

namespace XFramework.Integration.Wrappers
{
    public class RecordsWrapper : IRecordsWrapper
    {
        private IStreamFlowWrapper StreamFlowWrapper { get; }

        public RecordsWrapper(IStreamFlowWrapper streamFlowWrapper)
        {
            StreamFlowWrapper = streamFlowWrapper;
        }
        
        public async Task<Guid?> NewLog(string title, string message, Guid? guid = null, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
        {
            guid ??= Guid.NewGuid();
            
            await StreamFlowWrapper.Push(new StreamFlowMessageBO()
            {
                StreamFlowService = new StreamFlowServiceBO()
                {
                    Name = "RecordsService"
                },
                MethodName = "NewLog",
                Data = ""
            });
            return guid;
        }

        public Task<Guid?> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid)
        {
            throw new NotImplementedException();
        }

        public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog,
            GenericPriorityType priorityType = GenericPriorityType.Information)
        {
            throw new NotImplementedException();
        }
    }
}