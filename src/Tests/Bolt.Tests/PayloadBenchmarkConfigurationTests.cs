using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using NUnit.Framework;
using Perfolizer.Horology;

namespace Bolt.Tests;

[TestFixture]
public class PayloadBenchmarkConfigurationTests
{
    [Test]
    public void Config_UsesCredibleIterationSettings()
    {
        var job = new PayloadBenchConfig().GetJobs().Single();

        Assert.Multiple(() =>
        {
            Assert.That(job.Run.LaunchCount, Is.EqualTo(3));
            Assert.That(job.Run.WarmupCount, Is.EqualTo(5));
            Assert.That(job.Run.IterationCount, Is.GreaterThanOrEqualTo(15));
            Assert.That(job.Accuracy.MinIterationTime, Is.EqualTo(TimeInterval.FromMilliseconds(250)));
        });
    }

    [TestCaseSource(nameof(AllBenchmarkConfigs))]
    public void AllPerformanceConfigs_UseCredibleIterationSettings(ManualConfig config)
    {
        var job = config.GetJobs().Single();

        Assert.Multiple(() =>
        {
            Assert.That(job.Run.LaunchCount, Is.EqualTo(3));
            Assert.That(job.Run.WarmupCount, Is.EqualTo(5));
            Assert.That(job.Run.IterationCount, Is.GreaterThanOrEqualTo(15));
            Assert.That(job.Accuracy.MinIterationTime, Is.EqualTo(TimeInterval.FromMilliseconds(250)));
        });
    }

    private static IEnumerable<ManualConfig> AllBenchmarkConfigs()
    {
        yield return new BoltBenchConfig();
        yield return new PayloadBenchConfig();
        yield return new ThroughputBenchConfig();
        yield return new ConcurrencyBenchConfig();
        yield return new ScalabilityConfig();
    }

    [TestCase(nameof(PayloadBenchmarks.Bolt_Echo), nameof(PayloadBenchmarks.SetupBoltBenchmark))]
    [TestCase(nameof(PayloadBenchmarks.GRPC_Echo), nameof(PayloadBenchmarks.SetupGrpcBenchmark))]
    public void EachTransport_HasIsolatedGlobalSetup(string benchmarkName, string setupName)
    {
        var setup = typeof(PayloadBenchmarks).GetMethod(setupName)!;
        var attribute = setup.GetCustomAttributes(typeof(GlobalSetupAttribute), inherit: false)
            .Cast<GlobalSetupAttribute>()
            .Single();

        Assert.That(attribute.Targets, Is.EqualTo(new[] { benchmarkName }));
    }

    [Test]
    public void PayloadBenchmarks_DoNotUsePerIterationSetup()
    {
        var iterationSetups = typeof(PayloadBenchmarks)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(IterationSetupAttribute), inherit: false));

        Assert.That(iterationSetups, Is.Empty);
    }
}
