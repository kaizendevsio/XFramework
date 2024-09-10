using Blazor.IndexedDB.Framework;
using BlazorDownloadFile;
using Blazored.LocalStorage;
using Blazored.SessionStorage;
using BlazorState;
using CurrieTechnologies.Razor.SweetAlert2;
using MudBlazor.Services;
using XFramework.Blazor;
using XFramework.Blazor.Core.Services;
using XFramework.Blazor.Entity.Models.Components;

namespace ControlPanel.Core.Installers;

public class BlazorServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddBlazoredLocalStorage();
        services.AddBlazoredSessionStorage();
        services.AddMudServices();
        services.AddMudBlazorDialog();
        services.AddScoped<BlazorTransitionableRoute.IRouteTransitionInvoker, BlazorTransitionableRoute.DefaultRouteTransitionInvoker>();
        services.AddSweetAlert2(options => { options.Theme = SweetAlertTheme.Default; });
        services.AddBlazorState(o =>
        {
            o.Assemblies = new[]
            {
                typeof(Program).Assembly,
                typeof(ControlPanelCore).Assembly, 
                typeof(Base).Assembly
            };
            o.UseCloneStateBehavior = false;
        });
        services.AddScoped<IIndexedDbFactory, IndexedDbFactory>();
        services.AddScoped<IndexedDbService>();
        services.AddScoped<EndPointsModel>();
        services.AddBlazorDownloadFile();
    }
}