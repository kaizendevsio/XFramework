﻿
using HealthEssentials.Domain.Contexts;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthEssentials.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DbContext, HealthEssentialsContext>(options => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["HealthDatabaseConnection"])
                ? configuration.GetConnectionString("HealthDatabaseConnection")
                : configuration["HealthDatabaseConnection"])
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            );
    }
}