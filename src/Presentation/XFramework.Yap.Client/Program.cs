using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SqliteWasmBlazor;
using Yap.Client;
using Yap.Client.Services;
using Bolt.Media.Browser;

// The generated model's static constructor initializes itself on a 10 MB worker thread, which this
// single-threaded runtime cannot create: without this switch startup dies in OfflineDatabaseModel
// with PlatformNotSupportedException from Thread.Start. Must run before the model is first touched.
AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true);
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
builder.Services.AddSingleton<ILoggerProvider, DiagnosticsLoggerProvider>();
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress), Timeout = TimeSpan.FromSeconds(30) });
// UseModel swaps EF's reflection-driven model building for generated code. Regenerate it whenever
// OfflineDatabase changes: docs/solutions/developer-experience/yap-ef-compiled-model.md.
builder.Services.AddDbContextFactory<OfflineDatabase>(options => options
    .UseModel(OfflineDatabaseModel.Instance)
    .UseSqliteWasm(new SqliteWasmConnection("Data Source=Yap.db")));
builder.Services.AddSqliteWasm();
builder.Services.AddScoped<OfflineStore>();
builder.Services.AddScoped<ChatApi>();
// Scoped, not singleton, so its memo of formatted timestamps dies with the account's session.
builder.Services.AddScoped<BrowserTime>();
builder.Services.AddScoped<ChatState>();
builder.Services.AddScoped<VoiceState>();
builder.Services.AddBoltMediaBrowser(options => options.SecurityMode = Bolt.Media.Browser.MediaSecurityMode.AuthenticatedSFrame);
builder.Services.AddScoped<DatabaseStartup>();
var host = builder.Build();
// A breadcrumb must never be the reason Yap fails to open. Throwing here escapes Main before the
// root component exists, and the static splash spins on with nothing left to explain it.
try { await host.Services.GetRequiredService<IJSRuntime>().InvokeVoidAsync("yap.diagnostics.version", AppRelease.Version); }
catch (JSException) { }
await host.RunAsync();
