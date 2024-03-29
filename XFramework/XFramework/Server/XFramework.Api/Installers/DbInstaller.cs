﻿using Microsoft.EntityFrameworkCore.Diagnostics;

namespace XFramework.Api.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DbContext, AppDbContext>(options => options
            .UseNpgsql(string.IsNullOrEmpty(configuration["DefaultDatabaseConnection"])
                ? configuration.GetConnectionString("DefaultDatabaseConnection")
                : configuration["DefaultDatabaseConnection"])
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
        );
    }
}