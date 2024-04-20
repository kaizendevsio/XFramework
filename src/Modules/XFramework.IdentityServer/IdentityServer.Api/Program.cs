using IdentityServer.Domain.Shared.Contracts.Requests;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>()
    .Run(); 
