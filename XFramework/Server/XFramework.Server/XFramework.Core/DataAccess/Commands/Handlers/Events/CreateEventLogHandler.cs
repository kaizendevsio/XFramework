using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Events;
using XFramework.Core.Interfaces;
using XFramework.Domain.DataTableObjects;

namespace XFramework.Core.DataAccess.Commands.Handlers.Events
{
    public class CreateEventLogHandler : CommandBaseHandler, IRequestHandler<CreateEventLogCmd, bool>
    {
        public IConfiguration _configuration;
        public CreateEventLogHandler(IDataLayer dataLayer, IConfiguration configuration)
        {
            DataLayer = dataLayer;
            _configuration = configuration;
        }
        public async Task<bool> Handle(CreateEventLogCmd request, CancellationToken cancellationToken)
        {
            using (var transaction = DataLayer.Database.BeginTransaction())
            {
                try
                {
                    DataLayer.RollBack();
                    TblApplicationLogs applicationLogs = new TblApplicationLogs()
                    {
                        AppId = request.AppId,
                        Initiator = request.Initiator,
                        Severity = request.Severity,
                        Message = request.Message
                    };

                    DataLayer.Add(applicationLogs);
                    await DataLayer.SaveChangesAsync();

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    throw;
                }



            }

            return true;
        }
    }
}
