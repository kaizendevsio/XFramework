using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Application;


public partial class ApplicationState
{
    public class RestoreStates : BaseAction;
    
    protected class RestoreStatesHandler(HandlerServices handlerServices, IStore store) 
        : ActionHandler<RestoreStates>(handlerServices, store)
    {
        public override async Task Handle(RestoreStates action, CancellationToken aCancellationToken)
        {
            try
            { 
                var statePersistenceFromAppSettings = Configuration.GetValue<string>("Application:Persistence:State:Driver");
                var persistStateBy = (PersistStateBy)Enum.Parse(typeof(PersistStateBy), statePersistenceFromAppSettings);

                if (persistStateBy is PersistStateBy.IndexDb)
                {
                    await IndexedDbService.InitializeDb();
                }
                var tasks = new Task[3];
                
                tasks[1] = StateHelper.RestoreState(Mediator, IndexedDbService ,SessionStorageService, LocalStorageService,new SessionState.SetState() , SessionState, persistStateBy);
                tasks[2] = StateHelper.RestoreState(Mediator, IndexedDbService ,SessionStorageService, LocalStorageService,new WalletState.SetState() , WalletState, persistStateBy);

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