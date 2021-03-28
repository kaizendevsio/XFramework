using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.DataAccess.Commands.Entity.Events
{
   public class CreateEventLogCmd : TblApplicationLogs , IRequest<bool>
    {
        public string AppUID { get; set; }
    }
}
