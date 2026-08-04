using System.Reflection;
using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Extensions;

namespace XFramework.Core.DataContext;

public static class DataContextHandlerExtensions
{
    public static IServiceCollection AddDataContextHandler(this IServiceCollection services, Assembly entityAssembly)
    {
        services.AddTrustedInvocationSecurity();
        services.AddScoped<IQueryExecutionService>(sp =>
        {
            var queryService = ActivatorUtilities.CreateInstance<QueryExecutionService>(sp);

            var registrationType = entityAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataContextEntityRegistrations");

            if (registrationType is not null)
            {
                var method = registrationType.GetMethod("GetDataContextEntityTypes",
                    BindingFlags.Public | BindingFlags.Static);
                if (method?.Invoke(null, null) is Dictionary<string, Type> entityTypes)
                {
                    var mutableEntities = GetMutableEntityNames(registrationType);
                    foreach (var (name, type) in entityTypes)
                        queryService.RegisterEntity(type, name, mutableEntities?.Contains(name) ?? false);
                }
            }

            return queryService;
        });

        services.AddHostedService<DataContextBoltHandlerRegistration>();

        return services;
    }

    private static HashSet<string>? GetMutableEntityNames(Type registrationType)
    {
        var method = registrationType.GetMethod(
            "GetDataContextMutableEntityTypes",
            BindingFlags.Public | BindingFlags.Static);

        if (method?.Invoke(null, null) is HashSet<string> mutableEntityNames)
            return mutableEntityNames;

        return null;
    }

    private sealed class DataContextBoltHandlerRegistration(
        BoltClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<DataContextBoltHandler> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken ct)
        {
            new DataContextBoltHandler().Register(client, logger, scopeFactory);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
