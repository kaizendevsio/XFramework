namespace XFramework.Portal.Services;

public sealed class PortalBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PortalBootstrapHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new PortalAuthOptions();
        configuration.GetSection(PortalAuthOptions.BootstrapAdminSectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "Portal bootstrap admin seeding skipped because {Setting} is not configured.",
                "Portal:BootstrapAdmin:Password");
            return;
        }

        for (var attempt = 1; attempt <= 10 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<PortalBootstrapSeeder>();
                await seeder.SeedAsync(options, stoppingToken);
                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(
                    ex,
                    "Portal bootstrap admin seeding attempt {Attempt} failed. Retrying.",
                    attempt);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Portal bootstrap admin seeding failed.");
            }
        }
    }
}

