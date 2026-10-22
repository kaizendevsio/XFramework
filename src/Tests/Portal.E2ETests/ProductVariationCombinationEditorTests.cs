using System.Reflection;
using FluentAssertions;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Portal.Features.Inventario;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Unit"), Category("Module:Inventario"), Category("Area:PortalContract")]
public sealed class ProductVariationCombinationEditorTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void Draft_LoadAndEdit_DoesNotMutateSavedOptions()
    {
        var saved = new[]
        {
            new ProductVariationOption { ProductVariationTypeId = Guid.NewGuid(), Value = "Red" },
            new ProductVariationOption { ProductVariationTypeId = Guid.NewGuid(), Value = "Large" }
        };
        var draft = new ProductVariationCombinationDraft();
        draft.Load("Red large", 42m, saved);
        draft.Options[0].Value = "Blue";
        saved[0].Value.Should().Be("Red");
        draft.ToOptions()[0].Value.Should().Be("Blue");
        draft.Price.Should().Be(42m);
        draft.Load("", 10m);
        draft.Options.Should().HaveCount(2).And.OnlyContain(x => x.TypeId == Guid.Empty && x.Value == "");
    }

    [Test]
    public void Draft_ValidCombinationWithZeroPrice_AcceptsAbsolutePrice()
    {
        var draft = new ProductVariationCombinationDraft { Name = "Promotion", Price = 0 };
        foreach (var option in draft.Options)
        {
            option.TypeId = Guid.NewGuid();
            option.Value = "Value";
        }
        draft.Validate().Should().BeNull();
        draft.Price = -1;
        draft.Validate().Should().Contain("price");
        draft.Price = 1.001m;
        draft.Validate().Should().Contain("decimal places");
    }

    [Test]
    public void Editor_TypeChoices_ExcludeAlreadySelectedForeignDisabledAndDeletedTypes()
    {
        var product = Guid.NewGuid();
        var types = new[]
        {
            Type("Color"), Type("Size", product), Type("Other product", Guid.NewGuid()),
            Type("Disabled"), Type("Deleted")
        };
        types[3].IsEnabled = false;
        types[4].IsDeleted = true;
        var editor = new ProductVariationCombinationEditor { ProductId = product, Types = types };
        editor.Draft.Options[0].TypeId = types[0].Id;
        var choices = (IReadOnlyList<ProductVariationType>)Invoke(editor, "TypesFor", editor.Draft.Options[1])!;
        choices.Should().ContainSingle().Which.Id.Should().Be(types[1].Id);
        Invoke(editor, "SetType", editor.Draft.Options[1], types[0].Id.ToString());
        editor.Draft.Options[1].TypeId.Should().BeEmpty();
    }

    [Test]
    public void Editor_ChangeType_ClearsValueButPreservesOtherOptions()
    {
        var color = Type("Color");
        var size = Type("Size");
        var editor = new ProductVariationCombinationEditor { Types = [color, size] };
        var option = editor.Draft.Options[0];
        option.TypeId = color.Id;
        option.Value = "Red";
        editor.Draft.Options[1].Value = "Keep";
        Invoke(editor, "SetType", option, size.Id.ToString());
        option.TypeId.Should().Be(size.Id);
        option.Value.Should().BeEmpty();
        editor.Draft.Options[1].Value.Should().Be("Keep");
    }

    [Test]
    public void Editor_AddRemoveAndDisabled_RespectLimits()
    {
        var editor = new ProductVariationCombinationEditor();
        Invoke(editor, "RemoveOption", editor.Draft.Options[0]);
        editor.Draft.Options.Should().HaveCount(2);
        for (var i = 0; i < 10; i++)
            Invoke(editor, "AddOption");
        editor.Draft.Options.Should().HaveCount(ProductVariationCombination.MaxOptions);
        editor.Disabled = true;
        Invoke(editor, "RemoveOption", editor.Draft.Options[0]);
        editor.Draft.Options.Should().HaveCount(ProductVariationCombination.MaxOptions);
        editor.Disabled = false;
        Invoke(editor, "RemoveOption", editor.Draft.Options[0]);
        editor.Draft.Options.Should().HaveCount(ProductVariationCombination.MaxOptions - 1);
    }

    private static ProductVariationType Type(string name, Guid? product = null) =>
        new() { Id = Guid.NewGuid(), Name = name, ProductId = product, IsEnabled = true };

    private static object? Invoke(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, Private)!.Invoke(target, args);
}
