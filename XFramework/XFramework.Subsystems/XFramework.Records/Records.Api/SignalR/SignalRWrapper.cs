using System;
using System.Linq;
using MediatR;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Services;

namespace Records.Api.SignalR
{
    public class SignalRWrapper : SignalRService
    {
        public SignalRWrapper(StreamFlowConfiguration configuration, IMediator mediator) : base(configuration, mediator)
        {
        }

        public SignalRWrapper(IConfiguration configuration, IMediator mediator) : base(configuration, mediator)
        {
        }

        public override void Handle(IMediator mediator)
        {
            var installers = typeof(Startup).Assembly.ExportedTypes
                .Where(x => typeof(ISignalREventHandler).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(Activator.CreateInstance)
                .Cast<ISignalREventHandler>()
                .ToList();
            
            installers.ForEach(installer => installer.Handle(Connection, mediator));
            base.Handle(mediator);
        }

      
    }
}