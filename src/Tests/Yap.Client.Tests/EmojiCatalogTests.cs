using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Client.Tests;

public sealed class EmojiCatalogTests
{
    [Test]
    public void Insert_LandsAtTheCaretRatherThanTheEndOfTheDraft()
    {
        Assert.That(EmojiCatalog.Insert("hello world", 5, 5, "😊"), Is.EqualTo(("hello😊 world", 7)));
        Assert.That(EmojiCatalog.Insert("hello world", 0, 0, "😊"), Is.EqualTo(("😊hello world", 2)));
        Assert.That(EmojiCatalog.Insert("hello world", 11, 11, "😊"), Is.EqualTo(("hello world😊", 13)));
    }

    // A selection is what the caret is when the person has highlighted something, and the
    // emoji takes its place - same as typing a character would.
    [Test]
    public void Insert_ReplacesASelectionAndSurvivesABackwardsOne()
    {
        Assert.That(EmojiCatalog.Insert("hello world", 6, 11, "🌍"), Is.EqualTo(("hello 🌍", 8)));
        Assert.That(EmojiCatalog.Insert("hello world", 11, 6, "🌍"), Is.EqualTo(("hello 🌍", 8)));
    }

    // selectionStart counts UTF-16 code units, and so does a .NET string index, so an emoji
    // already in the draft must not shift where the next one lands.
    [Test]
    public void Insert_CountsTheSameUnitsTheTextareaReports()
    {
        var (text, caret) = EmojiCatalog.Insert("😊ok", 2, 2, "🎉");
        Assert.That(text, Is.EqualTo("😊🎉ok"));
        Assert.That(caret, Is.EqualTo(4));
        Assert.That(text[..caret], Is.EqualTo("😊🎉"));
    }

    [Test]
    public void Insert_ClampsACaretThatNoLongerFitsTheDraft()
    {
        Assert.That(EmojiCatalog.Insert("hi", 99, 99, "👋"), Is.EqualTo(("hi👋", 4)));
        Assert.That(EmojiCatalog.Insert("hi", -4, -4, "👋"), Is.EqualTo(("👋hi", 2)));
        Assert.That(EmojiCatalog.Insert(null, 0, 0, "👋"), Is.EqualTo(("👋", 2)));
    }

    [Test]
    public void Toggling_NeverTouchesTheDraft()
    {
        var keyboard = new EmojiKeyboardState();
        var draft = "hello world";
        Assert.That(keyboard.Open, Is.False);

        Assert.That(keyboard.Toggle(), Is.True);
        draft = keyboard.Insert(draft, 5, 5, "😊");
        Assert.That(draft, Is.EqualTo("hello😊 world"));
        var caret = keyboard.TakeCaret();
        Assert.That(caret, Is.EqualTo(7));
        // Taken once: a later render must not drag the caret back to a stale position.
        Assert.That(keyboard.TakeCaret(), Is.Null);

        Assert.That(keyboard.Toggle(), Is.False);
        keyboard.Close();
        Assert.That(keyboard.Toggle(), Is.True);
        Assert.That(draft, Is.EqualTo("hello😊 world"));

        draft = keyboard.Insert(draft, caret!.Value, caret.Value, "🎉");
        Assert.That(draft, Is.EqualTo("hello😊🎉 world"));
        Assert.That(keyboard.TakeCaret(), Is.EqualTo(9));
    }

    [Test]
    public void Groups_AreNonEmptyAndEveryEntryIsLabelled()
    {
        Assert.That(EmojiCatalog.Tabs, Is.Not.Empty);
        foreach (var (name, tab) in EmojiCatalog.Tabs)
        {
            Assert.That(EmojiCatalog.Group(name), Is.Not.Empty, name);
            Assert.That(tab, Is.Not.Empty);
        }
        Assert.That(EmojiCatalog.Group("Nonexistent"), Is.Empty);
        foreach (var emoji in EmojiCatalog.All)
        {
            Assert.That(emoji.Glyph, Is.Not.Empty);
            // The name is the accessible label for a button whose text is a picture.
            Assert.That(emoji.Name, Is.Not.Empty, emoji.Glyph);
            Assert.That(emoji.Name, Does.Not.Contain(";"), emoji.Glyph);
        }
        Assert.That(EmojiCatalog.All.Select(x => x.Glyph).Distinct().Count(), Is.EqualTo(EmojiCatalog.All.Count));
    }

    [TestCase("thumbs", "👍")]
    [TestCase("like", "👍")]
    [TestCase("cry", "😢")]
    [TestCase("lol", "🤣")]
    [TestCase("PIZZA", "🍕")]
    [TestCase("party pop", "🎉")]
    [TestCase("heart red", "❤️")]
    public void Search_MatchesAnyWordOfTheNameOrItsOtherSpellings(string query, string expected)
    {
        Assert.That(EmojiCatalog.Search(query).Select(x => x.Glyph), Does.Contain(expected));
    }

    [Test]
    public void Search_IsEmptyForNothingAndForNoMatch()
    {
        Assert.That(EmojiCatalog.Search(""), Is.Empty);
        Assert.That(EmojiCatalog.Search("   "), Is.Empty);
        Assert.That(EmojiCatalog.Search(null), Is.Empty);
        Assert.That(EmojiCatalog.Search("qwertzuiop"), Is.Empty);
    }

    // Recently used emoji come back from this device's storage as bare glyphs, with no label.
    [Test]
    public void Describe_NamesAKnownGlyphAndStillLabelsAnUnknownOne()
    {
        Assert.That(EmojiCatalog.Describe("🍕").Name, Is.EqualTo("pizza"));
        Assert.That(EmojiCatalog.Describe("🛼").Name, Is.EqualTo("Emoji"));
        Assert.That(EmojiCatalog.Describe("🛼").Glyph, Is.EqualTo("🛼"));
    }
}
