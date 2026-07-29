using BenchmarkDotNet.Running;
using Bolt.Tests;

try
{
    if (PayloadLatencyOptions.TryParse(args, out var latencyOptions))
        return await PayloadLatencyHarness.RunAsync(latencyOptions!);

    if (HubConnectionScaleOptions.TryParse(args, out var connectionScaleOptions))
        return await HubConnectionScaleHarness.RunAsync(connectionScaleOptions!);

    var informationalCommand = args.Any(static argument =>
        argument is "--help" or "-h" or "--list");
    var summaries = BenchmarkSwitcher.FromAssembly(typeof(BoltBenchmarks).Assembly).Run(args).ToArray();
    if (summaries.Length == 0)
    {
        if (informationalCommand)
            return 0;

        Console.Error.WriteLine("No benchmarks matched the supplied arguments.");
        return 2;
    }

    return summaries.Any(summary =>
        summary.HasCriticalValidationErrors ||
        summary.Reports.Any(report => !report.Success))
        ? 1
        : 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
