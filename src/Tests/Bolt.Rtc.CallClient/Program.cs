using Bolt.Media.Browser;
using Bolt.Rtc.CallClient;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.RootComponents.Add<App>("#app");
// Exactly the production registration (XFramework.Yap.Client/Program.cs).
builder.Services.AddBoltMediaBrowser(options => options.SecurityMode = MediaSecurityMode.AuthenticatedSFrame);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
await builder.Build().RunAsync();
