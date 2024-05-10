XApplication
    .Build<Program>()
    .UseCustomRequestsInAssembly<SmsGatewayBaseRequest>()
    .Run();