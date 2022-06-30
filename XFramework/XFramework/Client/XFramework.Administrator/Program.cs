var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddBlazorState(o =>
{
    o.Assemblies = new[] { typeof(Program).Assembly, typeof(BaseAction).Assembly, typeof(XFramework.Client.Shared.Core.Features.BaseAction).Assembly };
    o.UseCloneStateBehavior = false;
});
builder.Services.AddBlazoredSessionStorage();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<EndPointsModel>();

builder.Services.AddMediatR(typeof(BaseAction));
builder.Services.AddMediatR(typeof(Program));
builder.Services.AddScoped<BlazorTransitionableRoute.IRouteTransitionInvoker, BlazorTransitionableRoute.DefaultRouteTransitionInvoker>();

builder.Services.AddScoped<IHttpClient, HttpClientHelper>();
builder.Services.AddSweetAlert2(options => { options.Theme = SweetAlertTheme.Default; });
builder.Services.AddMudServices();

//builder.Services.AddValidatorsFromAssemblyContaining<BaseValidator>();
builder.Services.AddScoped(sp => new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});

builder.Services.AddSingleton<IIndexedDbFactory, IndexedDbFactory>();
builder.Services.AddSingleton<IndexedDbService>();

builder.Services.AddSingleton<ISignalRService, SignalRService>();
builder.Services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
builder.Services.AddSingleton<IIdentityServiceWrapper, IdentityServerDriver>();
builder.Services.AddSingleton<IWalletServiceWrapper, WalletServiceDriver>();
await builder.Build().RunAsync();