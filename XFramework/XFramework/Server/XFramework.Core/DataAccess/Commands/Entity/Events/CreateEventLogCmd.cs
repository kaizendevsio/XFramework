using MediatR;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.Events
{
   public class CreateEventLogCmd : TblApplicationLogs , IRequest<bool>
    {
        public string AppUID { get; set; }
    }
}
