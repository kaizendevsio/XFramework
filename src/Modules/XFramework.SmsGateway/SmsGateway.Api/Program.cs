using XFramework.Extensions;

XApplication
    .Build<Program>()
    .UseCustomRequestsInAssembly<SmsGatewayBaseRequest>()
    .Run();