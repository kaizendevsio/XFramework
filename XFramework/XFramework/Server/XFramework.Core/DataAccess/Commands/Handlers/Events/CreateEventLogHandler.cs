using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using XFramework.Core.DataAccess.Commands.Entity.Events;
using XFramework.Core.Interfaces;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Core.DataAccess.Commands.Handlers.Events
{
    public class CreateEventLogHandler : CommandBaseHandler, IRequestHandler<CreateEventLogCmd, bool>
    {
        public IConfiguration _configuration;
        public CreateEventLogHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<bool> Handle(CreateEventLogCmd request, CancellationToken cancellationToken)
        {
           return true;
        }
    }
}
