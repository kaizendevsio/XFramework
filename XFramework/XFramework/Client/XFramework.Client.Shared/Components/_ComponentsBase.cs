using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Modals;
using XFramework.Client.Shared.Core.Features.Session;

namespace XFramework.Client.Shared.Components;

public class XComponentsBase : BlazorStateComponent
{
    public ApplicationState AppState => GetState<ApplicationState>();
    public LayoutState LayoutState => GetState<LayoutState>();
    public SessionState SessionState => GetState<SessionState>();
    public ModalState ModalState => GetState<ModalState>();
    public CacheState CacheState => GetState<CacheState>();

    public string Cursor => AppState.IsBusy ? "progress" : "arrow";
}