XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .Run();
Serilog.Log.Information("Application started");