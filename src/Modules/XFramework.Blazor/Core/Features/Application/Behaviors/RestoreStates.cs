using XFramework.Blazor.Core.Features.Wallet;
using XFramework.Blazor.Entity.Enums;

namespace XFramework.Blazor.Core.Features.Application;


public partial class ApplicationState
{
    public record RestoreStates : StateAction;
    
    protected class RestoreStatesHandler(HandlerServices handlerServices, IStore store) 
        : StateActionHandler<RestoreStates>(handlerServices, store)
    {
        public override async Task Handle(RestoreStates action, CancellationToken aCancellationToken)
        {
            try
            { 
                var persistStateBy = StateHelper.GetPersistStateBy(Configuration);

                if (persistStateBy is PersistStateBy.IndexDb)
                {
                    await IndexedDbService.InitializeDb();
                }
                var tasks = new[]
                {
                    StateHelper.RestoreState(Mediator, IndexedDbService, SessionStorageService, LocalStorageService, new SessionState.SetState(), SessionState, persistStateBy),
                    StateHelper.RestoreState(Mediator, IndexedDbService, SessionStorageService, LocalStorageService, new WalletState.SetState(), WalletState, persistStateBy)
                };

                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            return;
        }
    }
}
