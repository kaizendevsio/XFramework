namespace XFramework.Bolt.Phase0Synthetics;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        var startedAtUtc = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        SyntheticReport report;
        try
        {
            var options = SyntheticOptionsParser.CreateDefault().Parse(args);
            report = await new BoltPhase0SyntheticRunner().RunAsync(options, shutdown.Token);
        }
        catch (Exception ex)
        {
            var completedAtUtc = DateTimeOffset.UtcNow;
            var code = ex switch
            {
                SyntheticConfigurationException configuration => configuration.Code,
                OperationCanceledException => "cancelled",
                _ => "unexpected_failure"
            };
            report = new SyntheticReport(
                SyntheticReportValidator.SchemaVersion,
                runId,
                new Dictionary<string, string>(),
                startedAtUtc,
                completedAtUtc,
                null,
                "failed",
                new SyntheticTimings((long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
                [
                    new SyntheticOperationResult(
                        "input_validation",
                        startedAtUtc,
                        completedAtUtc,
                        "failed",
                        (long)(completedAtUtc - startedAtUtc).TotalMilliseconds,
                        new Dictionary<string, string> { ["outcome"] = code })
                ]);
        }

        await SyntheticReportWriter.WriteAsync(Console.Out, report);
        return string.Equals(report.Status, "passed", StringComparison.Ordinal) ? 0 : 1;
    }
}
