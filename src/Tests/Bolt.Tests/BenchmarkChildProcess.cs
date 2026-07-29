using System.Diagnostics;

namespace Bolt.Tests;

internal sealed record BenchmarkChildProcessResult(
    int ExitCode,
    string StandardOutputPath,
    string StandardErrorPath);

internal static class BenchmarkChildProcess
{
    public static async Task<BenchmarkChildProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        string artifactDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        TimeSpan? timeout = null)
    {
        Directory.CreateDirectory(artifactDirectory);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
        startInfo.ArgumentList.Add(typeof(BenchmarkChildProcess).Assembly.Location);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var (name, value) in environment)
                startInfo.Environment[name] = value;
        }

        var stdoutPath = Path.Combine(artifactDirectory, "stdout.log");
        var stderrPath = Path.Combine(artifactDirectory, "stderr.log");
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start benchmark worker process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout ?? TimeSpan.FromMinutes(15));
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException(
                $"Benchmark worker exceeded its timeout. Logs: {Path.GetFullPath(artifactDirectory)}");
        }
        finally
        {
            await File.WriteAllTextAsync(stdoutPath, await stdout);
            await File.WriteAllTextAsync(stderrPath, await stderr);
        }

        return new BenchmarkChildProcessResult(process.ExitCode, stdoutPath, stderrPath);
    }
}
