namespace ControlPanel.Server.Services;

public sealed class ControlPanelBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ControlPanelBootstrapHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new ControlPanelAuthOptions();
        configuration.GetSection(ControlPanelAuthOptions.BootstrapAdminSectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "ControlPanel bootstrap admin seeding skipped because {Setting} is not configured.",
                "ControlPanel:BootstrapAdmin:Password");
            return;
        }

        for (var attempt = 1; attempt <= 10 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<ControlPanelBootstrapSeeder>();
                await seeder.SeedAsync(options, stoppingToken);
                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(
                    ex,
                    "ControlPanel bootstrap admin seeding attempt {Attempt} failed. Retrying.",
                    attempt);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ControlPanel bootstrap admin seeding failed.");
            }
        }
    }
}

