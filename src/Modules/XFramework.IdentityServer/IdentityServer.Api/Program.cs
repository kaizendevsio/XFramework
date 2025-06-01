XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>()
    .Run(); 
