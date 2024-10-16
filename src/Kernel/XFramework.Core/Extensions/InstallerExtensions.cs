﻿using System.Runtime.Versioning;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using XFramework.Core.Filters;
using XFramework.Core.Loggers;
using XFramework.Core.Middlewares;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Extensions;
using XFramework.Integration.PipelineBehaviours;
using XFramework.Integration.Services;
using Log = Serilog.Log;

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

    public static void InstallStreamflowRequestHandlers()
    {
        
    }
    
    public static void InstallSwagger(this IServiceCollection services, IConfiguration configuration)
    {
        // Swagger Versioning
        services.AddApiVersioning(options => {
            options.DefaultApiVersion = new ApiVersion(3, 0);
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = new HeaderApiVersionReader("api-version");
        });
        
        // Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\""
            });
                
            options.AddSecurityRequirement(new OpenApiSecurityRequirement{ 
                {
                    new OpenApiSecurityScheme{
                        Reference = new OpenApiReference{
                            Id = "Bearer", //The name of the previously defined security scheme.
                            Type = ReferenceType.SecurityScheme
                        }
                    },new List<string>()
                }
            });
        
            options.OperationFilter<ApiVersionOperationFilter>();
            options.OperationFilter<ODataOperationFilter>();
            options.SwaggerDoc("v3", new OpenApiInfo
            {
                Title = "XFramework API v3", 
                Version = "v3",
                Description = "XFramework API v3",
            });
        });
    }

    public static void InstallStandardServices<T>(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(XFrameworkCore).Assembly,
            typeof(T).Assembly
        ));
        services.AddValidatorsFromAssembly(typeof(RequestBase).GetTypeInfo().Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BasePipelineBehavior<,>));
        services.AddSingleton<ISignalRService, SignalRService>();
        services.AddSingleton<IHelperService, HelperService>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<CacheManager>();
        services.AddHttpClient();
        services.AddMemoryCache();
        services.AddAntiforgery();

        var seqUrl = configuration["SEQ_URL"];
        
        var loggerConfiguration = new LoggerConfiguration()
            .Enrich.FromLogContext() // This will ensure SourceContext is populated
            .Enrich.With(new ApplicationEnricher()) // U
            .WriteTo.Async(a => a.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} RequestId: {RequestId} => {Message:lj}{NewLine}{Exception}"))
            .WriteTo.Async(a => a.Seq(string.IsNullOrEmpty(seqUrl) 
                ? "http://localhost:5341"
                : seqUrl));
        
        Log.Logger = loggerConfiguration.CreateLogger();
        services.AddSingleton(Log.Logger);
        
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
        // JWT
        var jwtOptions = new JwtOptions();
        configuration.Bind(nameof(jwtOptions), jwtOptions);
        services.AddSingleton(jwtOptions);

        // Install JWT Authentication
        services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.ValidAudience,
                    ValidIssuer = jwtOptions.ValidIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.Secret)),
                    RequireExpirationTime = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
    }
    
    public static void AddTenantService(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
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
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v3/swagger.json", "XFramework API v3");
            // If you have more versions or groups, add them similarly:
            // c.SwaggerEndpoint("/swagger/v2/swagger.json", "Version 2 of My API");
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        });

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