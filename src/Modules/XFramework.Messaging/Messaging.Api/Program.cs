using XFramework.Extensions;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .UseCustomRequestsInAssembly<MessagingBaseRequest>()
    .Run();