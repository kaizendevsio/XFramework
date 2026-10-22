using System.Reflection;
using BlazorBlueprint.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using XFramework.Portal.Shared.Components;

namespace Portal.E2ETests;

[TestFixture, Category("Area:PortalContract")]
public sealed class DocumentSectionLayoutEditorTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Move_UsesCallbackAndDoesNotMutateInput_RespectingDisabledState(bool disabled)
    {
        var order = new List<string> { "summary", "items", "notes" };
        List<string>? updated = null;
        var editor = new DocumentSectionLayoutEditor<string>
        {
            Order = order, Disabled = disabled, Label = section => section,
            OrderChanged = EventCallback.Factory.Create<List<string>>(this, value => { updated = value; })
        };

        await Invoke(editor, "Move", "summary", 1);

        order.Should().Equal("summary", "items", "notes");
        if (disabled) updated.Should().BeNull();
        else updated.Should().Equal("items", "summary", "notes");
    }

    [TestCase("summary", -1)]
    [TestCase("notes", 1)]
    [TestCase("unknown", 1)]
    public async Task Move_BoundaryOrUnknownSection_DoesNotNotify(string section, int offset)
    {
        var calls = 0;
        var editor = new DocumentSectionLayoutEditor<string>
        {
            Order = ["summary", "items", "notes"], Label = value => value,
            OrderChanged = EventCallback.Factory.Create<List<string>>(this, _ => { calls++; })
        };
        await Invoke(editor, "Move", section, offset);
        calls.Should().Be(0);
    }

    [TestCase("notes", false, false, true)]
    [TestCase("notes", false, true, false)]
    [TestCase("items", false, false, false)]
    [TestCase("unknown", false, false, false)]
    [TestCase("notes", true, false, false)]
    public async Task Visibility_OptionalSectionsOnly_AndDisabledStateAreEnforced(string section, bool visible, bool disabled, bool changes)
    {
        var hidden = new List<string>();
        List<string>? updated = null;
        var editor = new DocumentSectionLayoutEditor<string>
        {
            Order = ["summary", "items", "notes"], Hidden = hidden, Disabled = disabled,
            Label = value => value, IsRequired = value => value == "items",
            HiddenChanged = EventCallback.Factory.Create<List<string>>(this, value => { updated = value; })
        };
        await Invoke(editor, "SetVisible", section, visible);
        hidden.Should().BeEmpty();
        if (changes) updated.Should().Equal("notes");
        else updated.Should().BeNull();
    }

    [Test]
    public async Task Visibility_ShowHiddenSection_NotifiesWithNewVisibleState()
    {
        var hidden = new List<string> { "notes" };
        List<string>? updated = null;
        var editor = new DocumentSectionLayoutEditor<string>
        {
            Order = ["notes"], Hidden = hidden, Label = value => value,
            HiddenChanged = EventCallback.Factory.Create<List<string>>(this, value => { updated = value; })
        };
        await Invoke(editor, "SetVisible", "notes", true);
        hidden.Should().Equal("notes");
        updated.Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public async Task Render_LabelsAndAccessibilityAreProvidedByConsumer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, StaticJavaScript>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<DocumentSectionLayoutEditor<string>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Order"] = new List<string> { "summary", "notes" },
                ["Label"] = (Func<string, string>)(section => section == "summary" ? "Overview" : "Remarks"),
                ["IsRequired"] = (Func<string, bool>)(section => section == "summary"),
                ["AriaLabel"] = "Invoice section order",
                ["VisibilityLabel"] = (Func<string, string>)(section => $"Show invoice {section}")
            }))).ToHtmlString());
        html.Should().Contain("Invoice section order").And.Contain("Show invoice summary")
            .And.Contain("Move Overview up").And.Contain("Move Remarks down").And.NotContain("receipt");
    }

    private static Task Invoke(object target, string method, params object[] args) =>
        (Task)target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args)!;

    private sealed class StaticJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new InvalidOperationException();
    }
}
