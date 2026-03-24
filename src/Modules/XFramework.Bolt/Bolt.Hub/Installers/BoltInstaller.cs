using MessagePack;
using Microsoft.AspNetCore.ResponseCompression;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Interfaces;

namespace Bolt.Hub.Installers;

/// <summary>
/// Installer for Bolt SignalR services with optimized MessagePack protocol configuration.
/// </summary>
public sealed class BoltInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Bind Bolt configuration early so it's available for SignalR options
        var streamFlowConfiguration = new BoltConfiguration();
        configuration.Bind(nameof(BoltConfiguration), streamFlowConfiguration);
        services.AddSingleton(streamFlowConfiguration);

        // Configure SignalR with MessagePack for binary serialization and performance optimization
        services.AddSignalR(options =>
        {
            // Allow large messages for bulk data transfer
            options.MaximumReceiveMessageSize = long.MaxValue;
            
            // Optimize for high-throughput scenarios
            options.EnableDetailedErrors = hostEnvironment.IsDevelopment();
            
            // Configure timeouts for better performance
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.HandshakeTimeout = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            
            // Allow concurrent hub method invocations per connection (default is 1 = sequential)
            options.MaximumParallelInvocationsPerClient = streamFlowConfiguration.MaxParallelInvocationsPerClient;

            // Optimize for streaming scenarios
            options.StreamBufferCapacity = 10;
        })
        .AddMessagePackProtocol(options =>
        {
            // Use LZ4 compression for optimal balance of speed and compression ratio
            options.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithSecurity(MessagePackSecurity.UntrustedData);
        });
            
        // Enable response compression for web sockets
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] { "application/octet-stream" });
        });
            
        // Bolt configuration already registered above
    }
}