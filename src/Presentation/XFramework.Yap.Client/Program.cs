using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SqliteWasmBlazor;
using Yap.Client;
using Yap.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress), Timeout = TimeSpan.FromSeconds(30) });
builder.Services.AddDbContextFactory<OfflineDatabase>(options => options.UseSqliteWasm(new SqliteWasmConnection("Data Source=Yap.db")));
builder.Services.AddSqliteWasm();
builder.Services.AddScoped<OfflineStore>();
builder.Services.AddScoped<ChatApi>();
builder.Services.AddScoped<ChatState>();
var startup = new DatabaseStartup();
builder.Services.AddSingleton(startup);
var host = builder.Build();
try
{
    await host.Services.GetRequiredService<IJSRuntime>().InvokeVoidAsync("yap.device.acquireDatabase");
    await host.Services.InitializeSqliteWasmAsync();
    await host.Services.InitializeSqliteWasmDatabaseAsync<OfflineDatabase>();
    await using var db = await host.Services.GetRequiredService<IDbContextFactory<OfflineDatabase>>().CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}
catch (Exception ex) { startup.Failed = true; Console.Error.WriteLine($"Yap database startup failed: {ex}"); }
await host.RunAsync();

public sealed class DatabaseStartup { public bool Failed { get; set; } }
