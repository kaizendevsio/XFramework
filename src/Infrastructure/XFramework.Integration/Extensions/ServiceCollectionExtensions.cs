using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Integration.Services;

namespace XFramework.Integration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDecoratorHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerType = typeof(IDecorator);
        var handlerImplementations = assembly.GetTypes()
            .Where(type => handlerType.IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });

        foreach (var handlerImplementation in handlerImplementations)
        {
            // Find ICommandHandler<,> or IQueryHandler<,> interface
            var interfaceType = handlerImplementation.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                     i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));
            
            if (interfaceType != null)
            {
                services.Decorate(interfaceType, handlerImplementation);
            }
        }

        return services;
    }
}

