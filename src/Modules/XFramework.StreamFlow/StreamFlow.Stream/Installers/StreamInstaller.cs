using MessagePack;
using Microsoft.AspNetCore.ResponseCompression;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Interfaces;

namespace StreamFlow.Stream.Installers;

public class StreamInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSignalR(o => o.MaximumReceiveMessageSize = long.MaxValue)
            .AddMessagePackProtocol(o => o.SerializerOptions.WithCompression(MessagePackCompression.Lz4BlockArray));
            
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/octet-stream" });
        });
            
        var streamFlowConfiguration = new StreamFlowConfiguration();
        configuration.Bind(nameof(streamFlowConfiguration), streamFlowConfiguration);
        services.AddSingleton(streamFlowConfiguration);
    }
}