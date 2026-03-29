using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Domain.Contexts;

namespace ControlPanel.Server.Services;

/// <summary>
/// Creates fresh AppDbContext instances for AdminDbContext.
/// Bypasses the IDbContextFactory issue with multiple constructors
/// by manually building DbContextOptions and calling the right constructor.
/// </summary>
public class AdminDbContextFactory(string connectionString, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
{
    public AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning))
            .Options;

        return new AppDbContext(options, httpContextAccessor, configuration);
    }
}
