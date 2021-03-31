using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Stream.Hubs.V1;

namespace StreamFlow.Stream.Installers
{
    public static class ConfigExtensions
    {
        public static void InstallEndpointConfigInAssembly(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessageQueueHub>("/hubs/v1/messageQueue/stream");
            });
            
        }
    }
}