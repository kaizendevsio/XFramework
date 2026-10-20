using BlazorBlueprint.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using XFramework.Portal.Shared.Components;
using XFramework.Portal.Shared.Services;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Area:PortalContract")]
public sealed class XfContainerPortalHostTests
{
    [Test]
    public async Task ContainerHost_ContentChangesDuringRender_ReplaysLatestContentOnRepeatedOpen()
    {
        await using var services = CreateServices();
        var portals = services.GetRequiredService<XfPortalService>();
        await using var renderer = new PortalRenderer(services);
        var rendered = new List<string>();
        var notifications = new List<string>();
        portals.OnPortalRendered += notifications.Add;
        await renderer.MountAsync();

        for (var pass = 0; pass < 3; pass++)
        {
            rendered.Clear();
            notifications.Clear();
            var latest = $"Latest content {pass}";
            RenderFragment updated = builder =>
            {
                rendered.Add(latest);
                builder.AddContent(0, latest);
            };

            await renderer.Dispatcher.InvokeAsync(() => portals.RegisterPortal("dialog", builder =>
            {
                rendered.Add("Opening");
                builder.AddContent(0, "Opening");
                // The host already visited this portal when a nested owner publishes new content.
                portals.UpdatePortalContent("dialog", updated);
            }, PortalCategory.Container));

            rendered.Should().Equal("Opening", latest);
            notifications.Should().Equal("dialog", "dialog");
            portals.GetPortals(PortalCategory.Container).Single().Value.Should().BeSameAs(updated);
            await renderer.Dispatcher.InvokeAsync(() => portals.UnregisterPortal("dialog"));
            portals.GetPortals(PortalCategory.Container).Should().BeEmpty();
        }
    }

    [Test]
    public async Task ContainerHost_OverlayChangesAreIsolated_AndDisposalDetachesHost()
    {
        await using var services = CreateServices();
        var portals = services.GetRequiredService<XfPortalService>();
        await using var renderer = new PortalRenderer(services);
        var renderCount = 0;
        await renderer.MountAsync();
        portals.HasHost.Should().BeTrue();
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            portals.RegisterPortal("dialog", builder =>
            {
                renderCount++;
                builder.AddContent(0, "Dialog");
            }, PortalCategory.Container);
            portals.RegisterPortal("select", builder => builder.AddContent(0, "Select"), PortalCategory.Overlay);
            portals.RefreshPortal("select");
            portals.UnregisterPortal("select");
        });
        renderCount.Should().Be(1, "overlay changes must not redraw dialog content");

        await renderer.DisposeAsync();
        portals.HasHost.Should().BeFalse();
        portals.RefreshPortal("dialog");
        renderCount.Should().Be(1, "the disposed host must no longer observe portal changes");
    }

    private static ServiceProvider CreateServices()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(runtime => runtime.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()))
            .ReturnsAsync(Mock.Of<IJSObjectReference>());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<XfPortalService>();
        services.AddSingleton(js.Object);
        return services.BuildServiceProvider();
    }

    private sealed class PortalRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        private int displayUpdates;
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public Task MountAsync() => Dispatcher.InvokeAsync(() =>
            RenderRootComponentAsync(AssignRootComponentId(InstantiateComponent(typeof(XfContainerPortalHost)))));

        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            if (++displayUpdates > 20)
                throw new InvalidOperationException("Container portal entered a render loop.");
            return Task.CompletedTask;
        }

        protected override void HandleException(Exception exception) => throw exception;
    }
}
