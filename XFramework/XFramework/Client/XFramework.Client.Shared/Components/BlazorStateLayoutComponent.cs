using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BlazorTransitionableRoute;

namespace XFramework.Client.Shared.Components;

public class BlazorStateLayoutComponent : LayoutComponentBase, IDisposable, IBlazorStateComponent
{
    private static readonly ConcurrentDictionary<string, int> s_InstanceCounts = new ConcurrentDictionary<string, int>();

    public BlazorStateLayoutComponent()
    {
        string name = this.GetType().Name;
        int num = BlazorStateLayoutComponent.s_InstanceCounts.AddOrUpdate(name, 1, (Func<string, int, int>) ((aKey, aValue) => aValue + 1));
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted(name);
        interpolatedStringHandler.AppendLiteral("-");
        interpolatedStringHandler.AppendFormatted<int>(num);
        this.Id = interpolatedStringHandler.ToStringAndClear();
    }

    public string Id { get; }

    [Parameter]
    public string TestId { get; set; }

    [Inject]
    public IMediator Mediator { get; set; }

    [Inject]
    public IStore Store { get; set; }

    [Inject]
    public Subscriptions Subscriptions { get; set; }

    public void ReRender() => this.StateHasChanged();

    protected T GetState<T>()
    {
        this.Subscriptions.Add(typeof (T), (IBlazorStateComponent) this);
        return this.Store.GetState<T>();
    }

    public virtual void Dispose()
    {
        this.Subscriptions.Remove((IBlazorStateComponent) this);
        GC.SuppressFinalize((object) this);
    }
}

public class TransitionableBlazorStateLayoutComponent : TransitionableLayoutComponent, IDisposable, IBlazorStateComponent
{
    private static readonly ConcurrentDictionary<string, int> s_InstanceCounts = new ConcurrentDictionary<string, int>();

    public TransitionableBlazorStateLayoutComponent()
    {
        string name = this.GetType().Name;
        int num = TransitionableBlazorStateLayoutComponent.s_InstanceCounts.AddOrUpdate(name, 1, (Func<string, int, int>) ((aKey, aValue) => aValue + 1));
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted(name);
        interpolatedStringHandler.AppendLiteral("-");
        interpolatedStringHandler.AppendFormatted<int>(num);
        this.Id = interpolatedStringHandler.ToStringAndClear();
    }

    public string Id { get; }

    [Parameter]
    public string TestId { get; set; }

    [Inject]
    public IMediator Mediator { get; set; }

    [Inject]
    public IStore Store { get; set; }

    [Inject]
    public Subscriptions Subscriptions { get; set; }

    public void ReRender() => this.StateHasChanged();

    protected T GetState<T>()
    {
        this.Subscriptions.Add(typeof (T), (IBlazorStateComponent) this);
        return this.Store.GetState<T>();
    }

    public virtual void Dispose()
    {
        this.Subscriptions.Remove((IBlazorStateComponent) this);
        GC.SuppressFinalize((object) this);
    }
}
