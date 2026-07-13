using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XFramework.Core.Middlewares;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;
using XFramework.Integration.Services;

namespace XFramework.Core.Extensions;

public static class InstallerExtensions
{
    private static ILogger<Application> _logger = null!;
    private static void DisplayRuntimeEnvironment()
    {
        _logger.LogInformation("Starting Application...");
        _logger.LogInformation("Application Name: {ApplicationName}", Assembly.GetEntryAssembly()?.GetName().Name?.Split(".")[0]);
        _logger.LogInformation("Application Version: {Version}", Assembly.GetEntryAssembly()?.GetName().Version);
        _logger.LogInformation("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        _logger.LogInformation("Machine Name: {MachineName}", Environment.MachineName);
        _logger.LogInformation("OS Version: {OsVersion}", Environment.OSVersion);
        _logger.LogInformation("Processor Count: {ProcessorCount}", Environment.ProcessorCount);
        _logger.LogInformation("Is 64 Bit Operating System: {Is64BitOperatingSystem}", Environment.Is64BitOperatingSystem);
        _logger.LogInformation("Is 64 Bit Process: {Is64BitProcess}", Environment.Is64BitProcess);
        _logger.LogInformation("Memory Footprint: {MemoryFootprint}", Environment.WorkingSet.Bytes());
        _logger.LogInformation("Timezone: {Timezone}", TimeZoneInfo.Local);
        _logger.LogInformation("Time Since Last Boot: {LastBoot}", TimeSpan.FromMilliseconds(Environment.TickCount64));
        _logger.LogInformation("Start Time: {StartTime}", DateTime.Now);
        _logger.LogInformation("Version: {Version}", Environment.Version);
        _logger.LogInformation("Runtime Version: {RuntimeVersion}", Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
    }

    public static void InstallBoltRequestHandlers()
    {
        
    }
    
    public static void InstallSwagger(this IServiceCollection services, IConfiguration configuration)
    {
        // API Versioning
        services.AddApiVersioning(options => {
            options.DefaultApiVersion = new ApiVersion(3, 0);
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = new HeaderApiVersionReader("api-version");
        });

        // .NET 10 built-in OpenAPI (replaces Swashbuckle)
        // ReferenceHandler.IgnoreCycles prevents circular reference errors in JSON responses
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

        services.AddOpenApi(options =>
        {
            options.CreateSchemaReferenceId = (type) => type.Type.Name;

            // Workaround for dotnet/aspnetcore#63857: JsonSchemaExporter can't handle
            // circular navigation properties in EF entities. Exclude any endpoint whose
            // request/response types reference entity models with navigation loops.
            // Services can opt specific endpoints out via .ExcludeFromDescription().
            // Source-generated entity CRUD endpoints are already excluded in the generator.
        });
    }

    public static void InstallStandardServices<T>(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(RequestBase).GetTypeInfo().Assembly);
        services.TryAddSingleton<IHelperService, HelperService>();
        services.TryAddSingleton<IJwtService, JwtService>();
        services.TryAddSingleton<CacheManager>();
        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddAntiforgery();

        XFrameworkExtensions.LoadMapsterDefaults();
    }
    
    public static void InstallRuntimeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(o => new DeviceAgentProvider(Environment.MachineName));
        _logger = services.BuildServiceProvider().GetRequiredService<ILogger<Application>>();
        DisplayRuntimeEnvironment();
    }
    
    public static void InstallOData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers().AddOData(o => o.EnableQueryFeatures()).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });
    }
    
    public static void InstallJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = new JwtOptions();
        configuration.Bind(nameof(jwtOptions), jwtOptions);
        JwtCredentialSet.Validate(jwtOptions, TimeProvider.System.GetUtcNow());

        services.AddSingleton(jwtOptions);
        services.TryAddSingleton(TimeProvider.System);
        services.AddCredentialGenerationHealthCheck();

        services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.SaveToken = true;
                x.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = BoltAccessTokenRedactionMiddleware.TakeAccessToken(context.HttpContext)
                            ?? context.Request.Query["access_token"].ToString();
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && path.StartsWithSegments("/bolt/ws"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
                x.TokenValidationParameters = JwtCredentialSet.CreateValidationParameters(jwtOptions, validateLifetime: true);
            });
    }
    
    public static void AddTenantResolver(this IServiceCollection services)
    {
        services.AddScoped<ITenantResolver, TenantResolver>();
    }
    
    public static void UseEndpointsInAssembly(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }

    public static void WarmUpServices(this IApplicationBuilder app, IServiceCollection services, ServiceLifetime serviceLifetime)
    {
        foreach (var service in GetLifetime(services, serviceLifetime))
        {
            // may be registered more than once, so get all at once
            app.ApplicationServices.GetServices(service);
        }
    }

    public static void UseFluentValidationExceptionHandler(this IApplicationBuilder application)
    {
        application.UseExceptionHandler(x =>
        {
            x.Run(async context =>
            {
                var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = errorFeature!.Error;
                string errorText = "";
                IEnumerable<(string, string)> errors = [];

                if (!(exception is ValidationException validationException))
                {
                    List<(string, string)> _error = new List<(string, string)>()
                    {
                        ("Exception", exception.Message),
                        ("InnerException", exception.InnerException != null ? exception.InnerException.Message : "")
                    };

                    errors = _error;
                }
                else
                {
                    errors = validationException.Errors.Select(err => (err.PropertyName, err.ErrorMessage));
                }

                errorText = JsonSerializer.Serialize(errors, new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles});
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(errorText, Encoding.UTF8).ConfigureAwait(true);
            });
        });
    }

    public static IApplicationBuilder UseConfiguredSwagger(this IApplicationBuilder app)
    {
        // OpenAPI + Scalar UI are mapped as endpoints in XApplication.Build()
        // (they require WebApplication.MapOpenApi which must be called after routing is configured)
        return app;
    }

    public static IApplicationBuilder UseCustomMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<RemoveEnhancedNavHeaderMiddleware>();
        
        return app;
    }
    
    public static IApplicationBuilder UseStandardMiddleware(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        if (env.IsProduction() || env.IsStaging())
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }
       
        app.UseCors(o => o.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
        app.UseRouting();
        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static IEnumerable<Type> GetLifetime(IServiceCollection services, ServiceLifetime serviceLifetime)
    {
        var s = services
            .Where(descriptor => descriptor.Lifetime == serviceLifetime)
            .Where(descriptor =>
                typeof(IXFrameworkService).IsAssignableFrom(descriptor
                    .ServiceType)) //&& !descriptor.ServiceType.IsInterface && !descriptor.ServiceType.IsAbstract)
            .Where(descriptor => descriptor.ServiceType.ContainsGenericParameters == false)
            .Select(descriptor => descriptor.ServiceType)
            .Distinct();

        return s;
    }
    
    public static void UseXFrameworkEndpoints(this WebApplication app)
    {
        app.MapGet("/startup", () =>
        {
            return new ApiStatus
            {
                ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name?.Split(".")[0]!,
                StartupTime = DateTime.Now,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!,
                Host = new Domain.Shared.BusinessObjects.Host
                {
                    Platform = Environment.OSVersion.Platform.ToString(),
                    MachineName = Environment.MachineName,
                    ProccessorCount = Environment.ProcessorCount,
                    Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                    Is64BitProccess = Environment.Is64BitProcess,
                    SystemPageSize = Environment.SystemPageSize,
                    TickCount64 = Environment.TickCount64,
                    Version = Environment.OSVersion.ToString(),
                    RuntimeVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName!
                },
                Status = "Running"
            };
        });
    }
}
