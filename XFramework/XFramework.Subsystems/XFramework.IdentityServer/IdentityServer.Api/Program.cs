using IdentityServer.Domain.Generic.Contracts.Requests;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>()
    .EnsureDatabase<DbContext>()
    .Run(); 