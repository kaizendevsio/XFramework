using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Extensions;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>()
    .Run(); 
