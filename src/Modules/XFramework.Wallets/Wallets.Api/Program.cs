using XFramework.Extensions;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .UseCustomRequestsInAssembly<WalletsBaseRequest>()
    .Run();