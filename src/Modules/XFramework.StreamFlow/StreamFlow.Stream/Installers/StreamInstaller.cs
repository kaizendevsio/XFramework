using MessagePack;
using Microsoft.AspNetCore.ResponseCompression;
using XFramework.Domain.Shared.Configurations;

namespace StreamFlow.Stream.Installers;

public class StreamInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
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