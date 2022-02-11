using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallets.Api.Installers;
using Wallets.Api.Options;

namespace Wallets.Api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }
    public IServiceCollection Services { get; set; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        Services = services;
        services.InstallServicesInAssembly(Configuration);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        var swaggerOptions = new SwaggerOptions();
        Configuration.GetSection(nameof(SwaggerOptions)).Bind(swaggerOptions);

        app.UseSwagger(options =>
        {
            options.RouteTemplate = swaggerOptions.JsonRoute;
        });

        app.UseSwaggerUI(c => c.SwaggerEndpoint($"v{swaggerOptions.Version}{swaggerOptions.UiEndpoint}", swaggerOptions.Description));

        /*app.UseHttpsRedirection();

        app.UseHsts();*/

        app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.InstallEndpointConfigInAssembly(env);

        app.WarmUpServices(Services, ServiceLifetime.Singleton);
    }
}