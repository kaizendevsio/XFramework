XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<HealthEssentialsBaseRequest>()
    .Run();