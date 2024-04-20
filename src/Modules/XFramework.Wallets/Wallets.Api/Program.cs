XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .UseCustomRequestsInAssembly<WalletsBaseRequest>()
    .Run();