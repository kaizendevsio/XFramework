namespace Bolt.Media.Browser;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoltMediaBrowser(
        this IServiceCollection services,
        Action<MediaServiceOptions>? configure = null)
    {
        var options = new MediaServiceOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<BoltCryptoInterop>();
        services.AddScoped<BoltAudioPipeline>();
        services.AddScoped<BoltVideoPipeline>();
        services.AddScoped<BoltDeviceManager>();
        services.AddScoped<BoltMediaService>();

        return services;
    }
}
