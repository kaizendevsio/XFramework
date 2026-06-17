using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public class QueryExecutionServiceTests
{
    private static readonly Guid DefaultTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    [Test]
    public async Task ExecuteAsync_FirstOrDefaultWithoutMatch_ReturnsSerializedNull()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var service = new QueryExecutionService(services, NullLogger<QueryExecutionService>.Instance);
        service.RegisterEntity<Tenant>("Tenant");

        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Mode = QueryExecutionMode.FirstOrDefault
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));

        resultBytes.Should().NotBeEmpty();
        MemoryPackSerializer.Deserialize<Tenant>(resultBytes).Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_MetadataRequestsIgnoreQueryFilters_ReturnsRowsOutsideDefaultTenant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var hiddenTenantId = Guid.NewGuid();
        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenant:DefaultId"] = DefaultTenantId.ToString()
                })
                .Build())
            .AddDbContext<FilteredQueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<FilteredQueryExecutionDbContext>())
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Add(new Tenant
            {
                Id = DefaultTenantId,
                TenantId = DefaultTenantId,
                Name = "Default Tenant",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            dbContext.Add(new Tenant
            {
                Id = hiddenTenantId,
                TenantId = hiddenTenantId,
                Name = "Hidden Tenant",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new QueryExecutionService(services, NullLogger<QueryExecutionService>.Instance);
        service.RegisterEntity<Tenant>("Tenant");

        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata
            {
                DeviceAgent = "ControlPanel;xframework.ignoreQueryFilters"
            }
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var tenants = MemoryPackSerializer.Deserialize<List<Tenant>>(resultBytes);

        tenants.Should().NotBeNull();
        tenants!.Select(x => x.Id).Should().Contain([DefaultTenantId, hiddenTenantId]);
    }

    private sealed class QueryExecutionDbContext(DbContextOptions<QueryExecutionDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = modelBuilder.Entity<Tenant>();
            tenant.HasKey(x => x.Id);
            tenant.Ignore(x => x.IdentityCredentials);
            tenant.Ignore(x => x.IdentityInformations);
            tenant.Ignore(x => x.IdentityRoleTypes);
            tenant.Ignore(x => x.RegistryConfigurations);
        }
    }

    private sealed class FilteredQueryExecutionDbContext(
        DbContextOptions<FilteredQueryExecutionDbContext> options,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
        : XDbContext(options, httpContextAccessor, configuration)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = modelBuilder.Entity<Tenant>();
            tenant.HasKey(x => x.Id);
            tenant.Ignore(x => x.IdentityCredentials);
            tenant.Ignore(x => x.IdentityInformations);
            tenant.Ignore(x => x.IdentityRoleTypes);
            tenant.Ignore(x => x.RegistryConfigurations);

            base.OnModelCreating(modelBuilder);
        }
    }
}
