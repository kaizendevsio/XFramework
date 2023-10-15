XApplication
    .Build<Program>()
    .GenerateMinimalApi(new[]
    {
        typeof(Tenant),
    }).Run();