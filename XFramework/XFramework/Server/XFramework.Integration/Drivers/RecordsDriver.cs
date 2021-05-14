using System;
using System.Threading.Tasks;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class RecordsDriver : ILoggerWrapper
    {
        private IMessageBusWrapper MessageBusWrapper { get; }

        public RecordsDriver(IMessageBusWrapper messageBusWrapper)
        {
            MessageBusWrapper = messageBusWrapper;
        }
        
        public async Task<Guid?> NewLog(string title, string message, Guid? guid = null, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
        {
            guid ??= Guid.NewGuid();
            
            /*await MessageBusWrapper.Push(new StreamFlowMessageBO()
            {
                StreamFlowService = new StreamFlowServiceBO()
                {
                    Name = "RecordsService"
                },
                MethodName = "NewLog",
                Data = ""
            });*/
            return guid;
        }

        public async Task<Guid?> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid)
        {
            //throw new NotImplementedException();
            return new();
        }

        public async Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog,
            GenericPriorityType priorityType = GenericPriorityType.Information)
        {
            //throw new NotImplementedException();
        }
    }
}