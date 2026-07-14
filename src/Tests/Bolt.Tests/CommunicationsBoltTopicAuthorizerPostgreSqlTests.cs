using System.Security.Claims;
using Bolt.Hub.Installers;
using Bolt.Hub.Services;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Integration.Abstractions;

namespace Bolt.Tests;

[TestFixture]
[NonParallelizable]
public sealed class CommunicationsBoltTopicAuthorizerPostgreSqlTests
{
    private const string ExternalConnectionStringEnvironmentVariable = "BOLT_TEST_POSTGRES_CONNECTION";
    private const string ExpectedDatabaseName = "bolt_authorization_test";

    private PostgreSqlContainer? postgres;
    private string connectionString = null!;

    [OneTimeSetUp]
    public async Task StartPostgreSql()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable(
            ExternalConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            connectionString = externalConnectionString;
        }
        else
        {
            try
            {
                postgres = new PostgreSqlBuilder()
                    .WithDatabase(ExpectedDatabaseName)
                    .WithUsername("bolt_test")
                    .WithPassword("bolt_test_password")
                    .Build();
                await postgres.StartAsync();
                connectionString = postgres.GetConnectionString();
            }
            catch (Exception ex) when (
                ex is ArgumentException or TypeInitializationException ||
                ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Bolt PostgreSQL tests require a Testcontainers-compatible Docker endpoint or a dedicated test database in {ExternalConnectionStringEnvironmentVariable}.",
                    ex);
            }
        }

        EnsureDedicatedTestDatabase(connectionString);

        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        await using var provider = CreateProvider(principal);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS \"Identity\"");
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS \"Communications\"");
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Identity"."IdentityCredential" (
                "ID" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "IsEnabled" boolean NOT NULL,
                "IsDeleted" boolean NOT NULL
            )
            """);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Communications"."MessageThreadMember" (
                "ID" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "MessageThreadId" uuid NOT NULL,
                "CredentialId" uuid NOT NULL,
                "IsEnabled" boolean NOT NULL,
                "IsDeleted" boolean NOT NULL
            )
            """);
    }

    [OneTimeTearDown]
    public async Task StopPostgreSql()
    {
        if (postgres is not null)
            await postgres.DisposeAsync();
    }

    [TestCase(true, false, false, true)]
    [TestCase(false, false, false, false)]
    [TestCase(true, true, false, false)]
    [TestCase(true, false, true, false)]
    public async Task ThreadSubscription_RequiresActiveMembershipInTopicTenant(
        bool memberEnabled,
        bool memberDeleted,
        bool wrongMemberTenant,
        bool expected)
    {
        var topicTenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var servicePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "XFramework.Proxy")],
            "Test"));
        var actorPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, credentialId.ToString("N")),
                new Claim("tenantId", topicTenantId.ToString("D"))
            ],
            "Test"));
        await using var provider = CreateProvider(servicePrincipal);

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbContext>();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Communications\".\"MessageThreadMember\"");
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Identity\".\"IdentityCredential\"");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Identity"."IdentityCredential" ("ID", "TenantId", "IsEnabled", "IsDeleted")
                VALUES ({credentialId}, {topicTenantId}, {true}, {false})
                """);
            var memberTenantId = wrongMemberTenant ? Guid.NewGuid() : topicTenantId;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Communications"."MessageThreadMember"
                    ("ID", "TenantId", "MessageThreadId", "CredentialId", "IsEnabled", "IsDeleted")
                VALUES ({Guid.NewGuid()}, {memberTenantId}, {threadId}, {credentialId}, {memberEnabled}, {memberDeleted})
                """);
        }

        var jwtService = Substitute.For<IJwtService>();
        jwtService.DecodeJwtToken("actor-token")
            .Returns(Task.FromResult((actorPrincipal, new System.IdentityModel.Tokens.Jwt.JwtSecurityToken())));
        var authorizer = new CommunicationsBoltTopicAuthorizer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            jwtService,
            NullLogger<CommunicationsBoltTopicAuthorizer>.Instance);
        var context = new BoltTopicAuthorizationContext(
            BoltTopicOperation.Subscribe,
            $"communications.tenant.{topicTenantId:N}.thread.{threadId:N}.typing",
            0,
            false,
            "client",
            "actor-token",
            "connection",
            "client",
            "Client",
            servicePrincipal);

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().Be(expected);
    }

    private ServiceProvider CreateProvider(ClaimsPrincipal httpPrincipal)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DefaultDatabaseConnection"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = httpPrincipal }
        });
        new DbInstaller().InstallServices<CommunicationsBoltTopicAuthorizerPostgreSqlTests>(
            services,
            configuration,
            Substitute.For<IHostEnvironment>());
        return services.BuildServiceProvider();
    }

    private static void EnsureDedicatedTestDatabase(string value)
    {
        var database = new NpgsqlConnectionStringBuilder(value).Database;
        if (!string.Equals(database, ExpectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Bolt PostgreSQL authorization tests run destructive setup and require database '{ExpectedDatabaseName}', but the configured database is '{database}'.");
        }
    }
}
