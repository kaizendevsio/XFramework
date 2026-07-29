using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Bolt.Tests;

internal sealed class BenchmarkServerHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private volatile bool _stopping;

    private BenchmarkServerHost(
        string name,
        WebApplication application,
        Task lifetimeTask,
        Uri baseAddress)
    {
        Name = name;
        _application = application;
        LifetimeTask = lifetimeTask;
        BaseAddress = baseAddress;
    }

    public string Name { get; }

    public Uri BaseAddress { get; }

    public Task LifetimeTask { get; }

    private bool IsStopping => _stopping;

    public static void ConfigureDynamicLoopback(
        WebApplicationBuilder builder,
        HttpProtocols protocols)
    {
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = protocols));
    }

    public static async Task<BenchmarkServerHost> StartAsync(
        string name,
        WebApplication application,
        TimeSpan? startupTimeout = null)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = application.Lifetime.ApplicationStarted.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            started);

        var lifetimeTask = application.RunAsync();
        Task completed;
        try
        {
            completed = await Task.WhenAny(started.Task, lifetimeTask)
                .WaitAsync(startupTimeout ?? TimeSpan.FromSeconds(15));
        }
        catch
        {
            await StopAndDisposeFailedStartAsync(application, lifetimeTask);
            throw;
        }

        if (ReferenceEquals(completed, lifetimeTask))
        {
            await ThrowServerExitAsync(name, lifetimeTask);
            throw new InvalidOperationException($"Benchmark server '{name}' exited during startup.");
        }

        var addresses = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;
        var address = addresses?.SingleOrDefault(static value =>
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

        if (address is null)
        {
            await StopAndDisposeFailedStartAsync(application, lifetimeTask);
            throw new InvalidOperationException(
                $"Benchmark server '{name}' did not publish one HTTP loopback address.");
        }

        return new BenchmarkServerHost(name, application, lifetimeTask, new Uri(address));
    }

    public void ThrowIfExited()
    {
        if (!_stopping && LifetimeTask.IsCompleted)
            ThrowServerExitAsync(Name, LifetimeTask).GetAwaiter().GetResult();
    }

    public static Task ObserveUnexpectedExitAsync(params BenchmarkServerHost[] hosts) =>
        ObserveUnexpectedExitCoreAsync(hosts);

    private static async Task ObserveUnexpectedExitCoreAsync(BenchmarkServerHost[] hosts)
    {
        if (hosts.Length == 0)
            await Task.Delay(Timeout.InfiniteTimeSpan);

        var completed = await Task.WhenAny(hosts.Select(static host => host.LifetimeTask));
        var host = hosts.Single(candidate => ReferenceEquals(candidate.LifetimeTask, completed));
        if (host.IsStopping)
            return;
        await ThrowServerExitAsync(host.Name, completed);
    }

    private static async Task ThrowServerExitAsync(string name, Task lifetimeTask)
    {
        try
        {
            await lifetimeTask;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Benchmark server '{name}' faulted.", exception);
        }

        throw new InvalidOperationException($"Benchmark server '{name}' stopped unexpectedly.");
    }

    private static async Task StopAndDisposeFailedStartAsync(
        WebApplication application,
        Task lifetimeTask)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await application.StopAsync(timeout.Token);
            await lifetimeTask.WaitAsync(timeout.Token);
        }
        catch
        {
            // Preserve the original startup failure.
        }
        finally
        {
            await application.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stopping)
            return;

        _stopping = true;
        await _application.StopAsync();
        await LifetimeTask;
        await _application.DisposeAsync();
    }
}

internal static class BenchmarkCallDeadline
{
    public static async Task<T> AwaitAsync<T>(
        Task<T> operation,
        CancellationTokenSource deadline,
        Task serverFailure,
        string operationName)
    {
        var deadlineSignal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = deadline.Token.UnsafeRegister(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            deadlineSignal);
        var completed = await Task.WhenAny(operation, serverFailure, deadlineSignal.Task);
        if (ReferenceEquals(completed, serverFailure))
        {
            deadline.Cancel();
            _ = ObserveCompletionAsync(operation);
            await serverFailure;
        }

        if (ReferenceEquals(completed, deadlineSignal.Task))
        {
            _ = ObserveCompletionAsync(operation);
            throw new TimeoutException($"{operationName} exceeded its logical benchmark deadline.");
        }

        try
        {
            return await operation;
        }
        catch (OperationCanceledException exception) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"{operationName} exceeded its logical benchmark deadline.", exception);
        }
    }

    private static async Task ObserveCompletionAsync(Task operation)
    {
        try
        {
            await operation;
        }
        catch
        {
        }
    }
}
