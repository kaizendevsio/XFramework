using XFramework.Core.Extensions;

var app = XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .EnsureDatabase<DbContext>()
    .UseCustomRequestsInAssembly<IdentityServerBaseRequest>();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.Run();
