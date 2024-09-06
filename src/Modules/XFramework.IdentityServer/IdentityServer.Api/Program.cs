using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Core.Extensions;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>()
    .Run(); 
