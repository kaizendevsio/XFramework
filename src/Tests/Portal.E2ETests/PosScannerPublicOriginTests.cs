using FluentAssertions;
using XFramework.Portal.Features.POS.Scanner;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
[Category("Module:POS")]
public sealed class PosScannerPublicOriginTests
{
    [TestCase("https://xeon-dev.tailed40e.ts.net:5000")]
    [TestCase("https://xeon-dev.tailed40e.ts.net:5000/")]
    public void Resolve_ConfiguredHttpsOrigin_AllowsHttpDesktop(string configured)
    {
        ScannerPublicOrigin.TryResolve(configured, "http://xeon-dev:5000/", out var origin).Should().BeTrue();
        new Uri(origin!, "/pos/mobile-scanner").AbsoluteUri.Should().Be("https://xeon-dev.tailed40e.ts.net:5000/pos/mobile-scanner");
    }

    [TestCase("http://xeon-dev:5000")]
    [TestCase("//xeon-dev.tailed40e.ts.net:5000")]
    [TestCase("https://user:password@xeon-dev:5000")]
    [TestCase("https://xeon-dev:5000?token=secret")]
    [TestCase("https://xeon-dev:5000#secret")]
    [TestCase("https://xeon-dev:5000/prefix")]
    [TestCase("javascript:alert(1)")]
    public void Resolve_InvalidConfiguredOrigin_RejectsWithoutFallingBack(string configured) =>
        ScannerPublicOrigin.TryResolve(configured, "https://desktop.example/", out _).Should().BeFalse();

    [Test]
    public void Resolve_UnconfiguredOrigin_RequiresHttpsDesktop()
    {
        ScannerPublicOrigin.TryResolve(null, "http://xeon-dev:5000/", out _).Should().BeFalse();
        ScannerPublicOrigin.TryResolve(null, "https://desktop.example/", out var origin).Should().BeTrue();
        origin!.Host.Should().Be("desktop.example");
    }
}
