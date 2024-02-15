﻿using Microsoft.EntityFrameworkCore.Diagnostics;

namespace StreamFlow.Stream.Installers;

public class DbInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DbContext, AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("DefaultDatabaseConnection"))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
        );
    }
}