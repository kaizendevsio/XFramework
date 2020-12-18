using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Events;
using XFramework.Core.Interfaces;
using XFramework.Domain.DTO;

namespace XFramework.Core.DataAccess.Commands.Handlers.Events
{
    public class CreateEventLogHandler : CommandBaseHandler, IRequestHandler<CreateEventLogCmd, bool>
    {
        public IConfiguration _configuration;
        public CreateEventLogHandler(IDataLayer dataLayer, IConfiguration configuration)
        {
            _dataLayer = dataLayer;
            _configuration = configuration;
        }
        public async Task<bool> Handle(CreateEventLogCmd request, CancellationToken cancellationToken)
        {
            using (var transaction = _dataLayer.Database.BeginTransaction())
            {
                try
                {
                    _dataLayer.RollBack();
                    TblApplicationLogs applicationLogs = new TblApplicationLogs()
                    {
                        AppId = request.AppId,
                        Initiator = request.Initiator,
                        Severity = request.Severity,
                        Message = request.Message
                    };

                    _dataLayer.Add(applicationLogs);
                    await _dataLayer.SaveChangesAsync();

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
