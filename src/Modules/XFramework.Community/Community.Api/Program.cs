using Community.Api.Generators;

XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .Run();