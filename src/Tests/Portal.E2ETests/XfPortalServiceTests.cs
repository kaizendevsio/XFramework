using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Area:PortalContract")]
public sealed class XfPortalServiceTests
{
    [Test]
    public void ScopedAlias_UsesSameServiceAndReportsChangedPortalId()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IPortalService, PortalService>();
        services.AddScoped<XfPortalService>();
        services.Replace(ServiceDescriptor.Scoped<IPortalService>(
            provider => provider.GetRequiredService<XfPortalService>()));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var portalService = scope.ServiceProvider.GetRequiredService<XfPortalService>();
        var portalServiceAlias = scope.ServiceProvider.GetRequiredService<IPortalService>();
        portalServiceAlias.Should().BeSameAs(portalService);

        var changes = new List<XfPortalChange>();
        portalService.OnPortalChanged += changes.Add;
        portalService.RegisterHost();

        RenderFragment content = builder => builder.AddContent(0, "Dialog content");
        portalServiceAlias.RegisterPortal("dialog-1", content, PortalCategory.Container);
        portalServiceAlias.RefreshPortal("dialog-1");
        portalServiceAlias.UnregisterPortal("dialog-1");

        changes.Should().Equal(
            new XfPortalChange("dialog-1", PortalCategory.Container),
            new XfPortalChange("dialog-1", PortalCategory.Container),
            new XfPortalChange("dialog-1", PortalCategory.Container));
    }

    [Test]
    public void GetPortals_PreservesRegistrationOrderAndUpdatedContent()
    {
        var portalService = new XfPortalService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<XfPortalService>.Instance);
        portalService.RegisterHost();

        RenderFragment firstContent = builder => builder.AddContent(0, "First");
        RenderFragment updatedFirstContent = builder => builder.AddContent(0, "Updated first");
        RenderFragment secondContent = builder => builder.AddContent(0, "Second");
        portalService.RegisterPortal("first", firstContent, PortalCategory.Container);
        portalService.RegisterPortal("second", secondContent, PortalCategory.Container);
        portalService.UpdatePortalContent("first", updatedFirstContent);

        var portals = portalService.GetPortals(PortalCategory.Container);
        portals.Select(portal => portal.Key).Should().Equal("first", "second");
        portals[0].Value.Should().BeSameAs(updatedFirstContent);
    }
}
