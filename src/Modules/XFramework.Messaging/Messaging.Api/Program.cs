using XFramework.Core.Extensions;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .UseCustomRequestsInAssembly<MessagingBaseRequest>()
    .Run();