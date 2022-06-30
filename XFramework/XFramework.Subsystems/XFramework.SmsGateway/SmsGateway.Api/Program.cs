namespace SmsGateway.Api;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseDefaultServiceProvider(options => options.ValidateScopes = false)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                #if !DEBUG
                webBuilder.UseSentry(o =>
                {
                    o.Dsn = "https://a3d9da6710bb43e895d7f2a4cf987f24@o1146205.ingest.sentry.io/6504224";
                    // When configuring for the first time, to see what the SDK is doing:
                    o.Debug = true;
                    // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring.
                    // We recommend adjusting this value in production.
                    o.TracesSampleRate = 1.0;
                });
                #endif
                
                webBuilder.UseStartup<Startup>(); 
            });
}