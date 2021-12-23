using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RBS.Core.Interfaces;
using RBS.Core.Services;

namespace RBS.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IJwtService, JwtService>();

            services.AddSignalR();
            services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/octet-stream" });
            });
        }
    }
}