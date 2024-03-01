using IdentityServer.Domain.Generic.Contracts.Requests;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>()
    .Run(); 
