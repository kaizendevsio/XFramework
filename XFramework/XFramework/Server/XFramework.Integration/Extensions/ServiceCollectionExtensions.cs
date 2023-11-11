using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

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
            var interfaceType = handlerImplementation.GetInterfaces()
                .First(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));
            services.Decorate(interfaceType, handlerImplementation);
        }

        return services;
    }
}

