using ControlPanel;
using XFramework.Extensions.WebAssembly;

await XApplication
    .Build<App>()
    .UseBlazor<App>()
    .RunAsync();

