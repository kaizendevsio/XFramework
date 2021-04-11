using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace StreamFlow.Stream
{
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
                    webBuilder.UseStartup<Startup>(); 
                    webBuilder.UseUrls("http://0.0.0.0:6000","https://0.0.0.0:6001");
                });
        
    }
}