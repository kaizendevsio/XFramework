using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Services;

namespace Messaging.Api.SignalR;

public class SignalRWrapper : SignalRService
{
    private readonly IMediator _mediator;

    public SignalRWrapper(StreamFlowConfiguration configuration) : base(configuration)
    {
    }

    public SignalRWrapper(IConfiguration configuration, IMediator mediator) : base(configuration,mediator)
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