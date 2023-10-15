using IdentityServer.Api.Extensions;
using XFramework.Api.Generators;
using XFramework.Domain.Generic.Contracts;

XApplication
    .Build<Program>()
    .UseAppServices()
    .GenerateMinimalApi(new[]
    {
        typeof(IdentityCredential),
    }).Run();