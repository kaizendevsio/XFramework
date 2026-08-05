using Bolt.Client;
using Bolt.Hub.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Integration.Drivers;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.POS)]
public sealed class PosBoltRuntimeIntegrationTests
{
    private const string BoltUrl = "http://localhost:17500";
    private const string PosUrl = "http://localhost:18700";
    private const string ClientUrl = "http://localhost:18701";
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid credentialId = Guid.NewGuid();
    private PostgreSqlContainer? postgres;
    private TestBoltTransportAuthority transportAuthority = null!;
    private TestInvocationIdentityOptions invocationIdentity = null!;
    private WebApplication? boltApp;
    private WebApplication? posApp;
    private WebApplication? clientApp;
    private IServiceScope? clientScope;
    private Guid registerId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
            postgres = new PostgreSqlBuilder()
                .WithDatabase("XFramework_POS_Bolt_Test")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();
            await postgres.StartAsync();
        }
        catch (ArgumentException exception) when (exception.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("POS Bolt integration tests require a Testcontainers-compatible Docker endpoint.");
        }

        registerId = Guid.NewGuid();
        transportAuthority = new TestBoltTransportAuthority(BoltUrl);
        var actorToken = TestInvocationIdentityExtensions.CreateTestActorToken(
            tenantId,
            credentialId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["Cashier"],
            [PosAuthorizationCapabilities.RegistersView]);
        invocationIdentity = new TestInvocationIdentityOptions(
            actorToken,
            "pos-runtime-service-token",
            XFrameworkServiceNames.Portal,
            tenantId,
            credentialId,
            Guid.NewGuid(),
            Guid.NewGuid());

        await MigrateAndSeedAsync();
        boltApp = StartBolt();
        posApp = StartPos();
        clientApp = StartClient();
        clientScope = clientApp.Services.CreateScope();

        await ConnectAsync(posApp.Services.GetRequiredService<BoltClient>());
        await ConnectAsync(clientApp.Services.GetRequiredService<BoltClient>());
        await Task.Delay(1000);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        clientScope?.Dispose();
        if (clientApp is not null) await clientApp.StopAsync();
        if (posApp is not null) await posApp.StopAsync();
        if (boltApp is not null) await boltApp.StopAsync();
        if (postgres is not null) await postgres.DisposeAsync();
        transportAuthority?.Dispose();
    }

    [Test]
    public async Task GetPosRegister_ThroughGeneratedBoltWrapper_EnforcesActorCapability()
    {
        var wrapper = clientScope!.ServiceProvider.GetRequiredService<IPOSServiceWrapper>();
        var request = new GetPosRegisterRequest
        {
            Id = registerId,
            Metadata = new RequestMetadata { RequestedTenantId = tenantId }
        };

        var allowed = await wrapper.GetPosRegister(request);

        allowed.IsSuccess.Should().BeTrue(allowed.Message);
        allowed.Response!.Id.Should().Be(registerId);

        var unauthorizedToken = TestInvocationIdentityExtensions.CreateTestActorToken(
            tenantId,
            credentialId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["Cashier"],
            []);
        using var tokenScope = TestInvocationActorTokenScope.Push(unauthorizedToken);

        var denied = await wrapper.GetPosRegister(request);

        denied.IsSuccess.Should().BeFalse();
        ((int)denied.HttpStatusCode).Should().Be(403);
    }

    private async Task MigrateAndSeedAsync()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PosRegister).TypeHandle);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres!.GetConnectionString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new AppDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(tenantId));
        await db.Database.MigrateAsync();
        db.Add(new PosRegister
        {
            Id = registerId,
            TenantId = tenantId,
            Name = "Bolt register",
            MerchantCredentialId = Guid.NewGuid(),
            CashDrawerWalletId = Guid.NewGuid(),
            WalletTypeId = Guid.NewGuid(),
            CurrencyId = Guid.NewGuid(),
            DefaultWarehouseId = Guid.NewGuid(),
            DefaultLocationId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
    }

    private WebApplication StartBolt()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls(BoltUrl);
        OverrideConfig(builder, "Bolt.PosRuntimeTest", BoltUrl);
        transportAuthority.Configure(builder);
        builder.Services.InstallServicesInAssembly<Bolt.Hub.Installers.BoltInstaller>(builder.Configuration, builder.Environment);
        builder.Services.InstallSwagger(builder.Configuration);
        builder.Services.InstallOData(builder.Configuration);
        builder.Services.InstallJwt(builder.Configuration);
        builder.Services.InstallStandardServices<Bolt.Hub.Installers.BoltInstaller>(builder.Configuration);
        builder.Services.InstallRuntimeServices(builder.Configuration);
        builder.Services.AddTestInvocationClient(invocationIdentity);

        var app = (WebApplication)builder.Build();
        transportAuthority.MapEndpoints(app);
        app.UseCorrelationId();
        app.UseAppServices();
        app.Start();
        return app;
    }

    private WebApplication StartPos()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls(PosUrl);
        OverrideConfig(builder, XFrameworkServiceNames.Pos, PosUrl);
        builder.Configuration["ConnectionStrings:DefaultDatabaseConnection"] = postgres!.GetConnectionString();
        builder.Configuration["DefaultDatabaseConnection"] = postgres.GetConnectionString();
        builder.Services.InstallStandardServices<PosRegisterService>(builder.Configuration);
        builder.Services.AddTestInvocationServer(invocationIdentity);
        builder.Services.AddSingleton(transportAuthority.CreateTokenProvider(XFrameworkServiceNames.Pos));

        var featureGate = new Mock<ITrustedInvocationFeatureGate>();
        featureGate
            .Setup(gate => gate.EnsureAllowedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        builder.Services.AddSingleton(featureGate.Object);

        var app = (WebApplication)builder.Build();
        POS.Api.Generated.BoltHandlerRegistry.RegisterAll(
            app.Services.GetRequiredService<BoltClient>(),
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("POS.RuntimeTests"),
            app.Services.GetRequiredService<IServiceScopeFactory>());
        app.Start();
        return app;
    }

    private WebApplication StartClient()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls(ClientUrl);
        OverrideConfig(builder, XFrameworkServiceNames.Portal, ClientUrl);
        builder.Services.InstallStandardServices<PosBoltRuntimeIntegrationTests>(builder.Configuration);
        builder.Services.AddXFrameworkBoltClient(builder.Configuration, autoConnect: false);
        builder.Services.AddSingleton(transportAuthority.CreateTokenProvider(XFrameworkServiceNames.Portal));
        builder.Services.AddTestInvocationClient(invocationIdentity);
        builder.Services.AddPOSWrapperServices();

        var app = builder.Build();
        app.Start();
        return app;
    }

    private static async Task ConnectAsync(BoltClient client)
    {
        if (!client.IsConnected)
            await client.ConnectWithRetryAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);
    }

    private static void OverrideConfig(WebApplicationBuilder builder, string clientName, string url)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = clientName,
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{BoltUrl}/bolt/ws",
            ["ServiceIdentity:ClientId"] = clientName,
            ["ServiceIdentity:Authority"] = BoltUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true",
            ["ServiceIdentity:GenerationId"] = "pos-runtime-tests-g1",
            ["ServiceIdentity:ClientSecret"] = "pos-runtime-tests-secret-2026-secure",
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["Tenant:DefaultId"] = TestConstants.TenantId.ToString(),
            ["Kestrel:Endpoints:Http:Url"] = url,
            ["urls"] = url,
            ["Logging:LogLevel:Default"] = "Warning"
        });
    }
}
