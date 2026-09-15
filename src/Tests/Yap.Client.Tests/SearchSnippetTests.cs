using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Client.Tests;

public sealed class SearchSnippetTests
{
    private static string Plain(string? text, string? query, int budget = SearchSnippet.Budget) =>
        SearchSnippet.Plain(SearchSnippet.Build(text, query, budget));

    private static string[] Matches(string? text, string? query, int budget = SearchSnippet.Budget) =>
        SearchSnippet.Build(text, query, budget).Where(x => x.Match).Select(x => x.Text).ToArray();

    [Test]
    public void Build_MarksTheMatchAndKeepsItsOriginalCasing()
    {
        Assert.That(Matches("Can you take a look at the Onboarding flow?", "onboarding"), Is.EqualTo(new[] { "Onboarding" }));
        Assert.That(Plain("Can you take a look at the Onboarding flow?", "onboarding"),
            Is.EqualTo("Can you take a look at the Onboarding flow?"));
    }

    [Test]
    public void Build_MarksEveryOccurrenceInsideTheExcerpt()
    {
        Assert.That(Matches("ship it, then ship it again", "ship"), Is.EqualTo(new[] { "ship", "ship" }));
    }

    [Test]
    public void Build_TrimsAroundAMatchBuriedInALongMessage()
    {
        var text = new string('a', 400) + " needle " + new string('b', 400);
        var segments = SearchSnippet.Build(text, "needle");
        Assert.Multiple(() =>
        {
            Assert.That(SearchSnippet.Plain(segments), Does.StartWith("…").And.EndWith("…"));
            Assert.That(SearchSnippet.Plain(segments), Does.Contain("needle"));
            // The excerpt stays within the two-line budget plus its two ellipses.
            Assert.That(SearchSnippet.Plain(segments).Length, Is.LessThanOrEqualTo(SearchSnippet.Budget + 2));
        });
    }

    [Test]
    public void Build_KeepsLeadingContextWhenTheMatchIsNearTheStart()
    {
        var segments = SearchSnippet.Build("Hello there needle " + new string('b', 300), "needle");
        Assert.That(SearchSnippet.Plain(segments), Does.StartWith("Hello there needle"));
    }

    [Test]
    public void Build_CollapsesNewlinesSoARowStaysTwoLines()
    {
        Assert.That(Plain("first line\n\n  second\tline", "second"), Is.EqualTo("first line second line"));
    }

    [Test]
    public void Build_KeepsTheWholeMatchEvenWhenTheQueryIsLongerThanTheBudget()
    {
        var query = new string('x', 40);
        var segments = SearchSnippet.Build(new string('a', 60) + query + new string('b', 60), query, budget: 20);
        Assert.That(Matches(new string('a', 60) + query + new string('b', 60), query, budget: 20), Is.EqualTo(new[] { query }));
        Assert.That(SearchSnippet.Plain(segments), Does.Contain(query));
    }

    [Test]
    public void Build_NeverSplitsASurrogatePair()
    {
        // A budget that lands mid-emoji must step back rather than emit half a code point.
        var segments = SearchSnippet.Build("ship \U0001F680\U0001F680\U0001F680\U0001F680 done", "ship", budget: 7);
        var plain = SearchSnippet.Plain(segments);
        Assert.That(plain.Where(char.IsHighSurrogate).Count(), Is.EqualTo(plain.Where(char.IsLowSurrogate).Count()));
    }

    [Test]
    public void Build_WithoutAMatchStillRendersAClippedHead()
    {
        Assert.That(Plain("a short message", "zzz"), Is.EqualTo("a short message"));
        Assert.That(Plain(new string('a', 400), ""), Does.EndWith("…"));
        Assert.That(SearchSnippet.Build("", "ship"), Is.Empty);
    }
}
