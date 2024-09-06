using XFramework.Core.Extensions;

XApplication
    .Build<Program>()
    .UseCustomRequestsInAssembly<SmsGatewayBaseRequest>()
    .Run();