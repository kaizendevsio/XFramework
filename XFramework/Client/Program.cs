using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace Ark.Mobile
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            Uri uri = new Uri("https://localhost:55007/");
            builder.RootComponents.Add<App>("app");

            if (!builder.HostEnvironment.BaseAddress.Contains("localhost"))
            {
                uri = new Uri("http://ec2-54-179-21-53.ap-southeast-1.compute.amazonaws.com:8089/");
            }

            builder.Services.AddTransient(sp =>
            new HttpClient
            {
                BaseAddress = uri
                //BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            });

            await builder.Build().RunAsync();
        }
    }
}
