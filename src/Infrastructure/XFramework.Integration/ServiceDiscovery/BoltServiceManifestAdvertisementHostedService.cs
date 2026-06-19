using Bolt.Client;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XFramework.Integration.ServiceDiscovery;

public sealed class BoltServiceManifestAdvertisementHostedService(
    BoltClient client,
    IEnumerable<IBoltServiceManifestProvider> manifestProviders,
    IOptions<BoltServiceDiscoveryOptions> options,
    ILogger<BoltServiceManifestAdvertisementHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshSeconds = Math.Max(5, options.Value.ManifestRefreshSeconds);
        var refreshInterval = TimeSpan.FromSeconds(refreshSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitForConnectionAsync(stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await AdvertiseAsync(stoppingToken);
            await Task.Delay(refreshInterval, stoppingToken);
        }
    }

    private async Task WaitForConnectionAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !client.IsConnected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
    }

    private async Task AdvertiseAsync(CancellationToken ct)
    {
        try
        {
            var manifest = await BuildManifestAsync(ct);
            if (manifest is null)
            {
                return;
            }

            var response = await client.SendAsync<BoltServiceManifest, BoltServiceManifestAdvertisementResponse>(
                string.Empty,
                BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
                manifest,
                ct);

            if (response is { Accepted: false })
            {
                logger.LogWarning("Bolt service manifest rejected: {Message}", response.Message);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Skipping Bolt service manifest advertisement while disconnected");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to advertise Bolt service manifest");
        }
    }

    private async ValueTask<BoltServiceManifest?> BuildManifestAsync(CancellationToken ct)
    {
        BoltServiceManifest? merged = null;

        foreach (var provider in manifestProviders)
        {
            var manifest = await provider.GetManifestAsync(ct);
            if (manifest is null)
            {
                continue;
            }

            if (merged is null)
            {
                merged = manifest;
                continue;
            }

            merged.Modules.AddRange(manifest.Modules);
            merged.Dependencies.AddRange(manifest.Dependencies);
            foreach (var (key, value) in manifest.Metadata)
            {
                merged.Metadata.TryAdd(key, value);
            }
        }

        return merged;
    }
}
