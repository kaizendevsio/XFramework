using XFramework.Core.Extensions;

var app = XApplication
    .Build<Program>()
    .UseAppServices();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.Run();