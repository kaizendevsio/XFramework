using MediatR;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using Wallets.Api.Installers;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Services;

namespace Wallets.Api.SignalR
{
    public class SignalRWrapper : SignalRService
    {
        private readonly IMediator _mediator;

        public SignalRWrapper(StreamFlowConfiguration configuration) : base(configuration)
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