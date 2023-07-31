using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace StreamFlow.Stream;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseSentry(o =>
                {
                    o.Dsn = "https://f160d850e62148bd9e278c22fdc4c104@o1146205.ingest.sentry.io/6504187";
                    // When configuring for the first time, to see what the SDK is doing:
                    o.Debug = true;
                    // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring.
                    // We recommend adjusting this value in production.
                    o.TracesSampleRate = 1.0;
                });

                webBuilder.UseStartup<Startup>(); 
            });
        
}