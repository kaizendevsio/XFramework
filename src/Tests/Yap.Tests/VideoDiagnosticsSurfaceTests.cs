using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Yap.Client.Components;

namespace Yap.Tests;

public sealed class VideoDiagnosticsSurfaceTests
{
    [Test]
    public async Task RecoveryAndBudgetLabels_DistinguishCountersFromRatesAndOpusFromWireBudget()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        var snapshot = new VideoDiagnostics
        {
            TotalBudgetKbps = 512, VideoBudgetKbps = 373, AudioBitrateKbps = 32, Congestion = "Hold", QueueDelayMs = 120,
            UplinkResent = 8,
            Remotes = [new() { StreamId = Guid.NewGuid(), Recovery = new(Guid.NewGuid(), 100, 4, 10, 6, 2, 1, 3, 5, 20, 1050) }]
        };
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<VideoDiagnosticsPanel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(VideoDiagnosticsPanel.Snapshot)] = snapshot }))).ToHtmlString());
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("512 kbps total / 373 video / 32 Opus"));
            Assert.That(html, Does.Contain("counters since stream start"));
            Assert.That(html, Does.Contain("8 fragments since transport start"));
            Assert.That(html, Does.Contain("20 released / 3 incomplete / 5 skipped"));
            Assert.That(html, Does.Contain("delays are local codec timings"));
        });
    }
}
