using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Yap.Client.Tests;

/// <summary>
/// The published client runs with InvariantGlobalization and no timezone database, so inside WASM
/// <c>TimeZoneInfo.Local</c> is UTC and <c>CurrentCulture</c> is invariant. A reintroduced
/// <c>ToLocalTime</c> or culture-sensitive format therefore does not throw - it quietly prints the
/// wrong hour, in the wrong order, for every person who is not on UTC. No runtime test can see
/// that, because the wrong answer is a well-formed string, so the guard runs over the source: that
/// also covers the .razor markup this test project never compiles, which is where most of the
/// original offenders lived. A line that genuinely needs a bare overload opts out with a trailing
/// <c>// culture-ok: reason</c> comment.
/// </summary>
public sealed class GlobalizationGuardTests
{
    private static readonly string[] Roots =
    [
        "Presentation/XFramework.Yap.Client",
        "Presentation/XFramework.Yap.Contracts"
    ];

    /// <summary>Anything here reads the machine clock's zone or the ambient culture. In WASM both are wrong.</summary>
    private static readonly (string Token, string Instead)[] Banned =
    [
        ("ToLocalTime(", "pass the UTC instant to BrowserTime, which formats it with Intl"),
        ("ToUniversalTime(", "keep DateTimes in UTC instead of converting at the edge"),
        ("TimeZoneInfo", "the runtime ships no timezone data; the browser knows the real zone"),
        ("DateTime.Now", "use DateTime.UtcNow, and BrowserTime for anything a person reads"),
        ("DateTimeOffset.Now", "use DateTimeOffset.UtcNow"),
        ("DateTime.Today", "\"today\" is a local-calendar question; yap.time answers it"),
        ("ToShortTimeString(", "BrowserTime.Clock"),
        ("ToLongTimeString(", "BrowserTime.Clock"),
        ("ToShortDateString(", "BrowserTime.DayMonth"),
        ("ToLongDateString(", "BrowserTime.DaySeparator"),
        ("CultureInfo.CurrentCulture", "there is only the invariant culture in the published app"),
        ("CultureInfo.CurrentUICulture", "there is only the invariant culture in the published app"),
        ("new CultureInfo(", "no ICU data is shipped, so a named culture cannot be constructed"),
        (".ToLower()", "ToLowerInvariant(), or an OrdinalIgnoreCase comparison"),
        (".ToUpper()", "ToUpperInvariant(), or an OrdinalIgnoreCase comparison")
    ];

    /// <summary>Formats that are defined to ignore the culture, so they stay safe with no ICU.</summary>
    private static readonly HashSet<string> RoundTrip = ["O", "o", "s", "R", "r", "u"];

    /// <summary>A single-argument ToString on something date-shaped: culture-sensitive unless round-trip.</summary>
    private static readonly Regex DateToString = new(
        """(CreatedAt|SavedAt|StartedAt|EndedAt|UpdatedAt|LastMessageAt|LastActiveAt|ActiveUntil|ExpiresAt|ConnectedAt|Timestamp|DateTime|DateTimeOffset|\bdate\b|\blocal\b)[^;"]{0,60}\.ToString\("([^"]*)"\)""",
        RegexOptions.Compiled);

    /// <summary>StartsWith/EndsWith/IndexOf against a literal default to a linguistic comparison.</summary>
    private static readonly Regex LinguisticCompare = new(
        """\.(StartsWith|EndsWith|IndexOf|LastIndexOf)\("(?:[^"\\]|\\.)*"([^)]*)\)""",
        RegexOptions.Compiled);

    private static string PresentationRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static IEnumerable<string> Sources() => Roots
        .Select(root => Path.Combine(PresentationRoot, root))
        .SelectMany(root => Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IEnumerable<(string File, int Number, string Text)> Lines() => Sources()
        .SelectMany(path => File.ReadAllLines(path).Select((text, index) => (Path.GetFileName(path), index + 1, Code(text))));

    /// <summary>A banned name written in a comment is prose, not a call - and these are exactly the APIs
    /// the comments here have to name to explain themselves. Everything up to a line comment is kept;
    /// "://" is left alone so a URL does not truncate the code beside it. A line opting out with
    /// "// culture-ok" drops whole, which is the point of the marker.</summary>
    private static string Code(string line)
    {
        if (line.Contains("// culture-ok", StringComparison.Ordinal)) return "";
        for (var i = line.IndexOf("//", StringComparison.Ordinal); i >= 0; i = line.IndexOf("//", i + 2, StringComparison.Ordinal))
            if (i == 0 || line[i - 1] != ':') return line[..i];
        return line;
    }

    /// <summary>A guard that scans no files passes for the wrong reason, and would go on passing while
    /// the bug it exists to catch shipped. This is the one assertion that fails if the walk breaks.</summary>
    [Test]
    public void TheScanActuallyReachesTheClient()
    {
        var files = Sources().Select(Path.GetFileName).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(files, Has.Length.GreaterThan(50), "The source walk found almost nothing - the path is wrong.");
            foreach (var expected in new[] { "MessageWindow.razor", "MessageSearch.razor", "Inbox.razor", "Saved.razor", "ConversationPage.razor", "ChatModels.cs", "BrowserTime.cs" })
                Assert.That(files, Does.Contain(expected));
        });
    }

    [Test]
    public void ClientNeverReadsTheLocalClockOrTheAmbientCulture()
    {
        var found = Lines()
            .SelectMany(line => Banned.Where(rule => line.Text.Contains(rule.Token, StringComparison.Ordinal))
                .Select(rule => $"{line.File}:{line.Number} uses {rule.Token} - use {rule.Instead}"))
            .ToArray();
        Assert.That(found, Is.Empty, string.Join(Environment.NewLine, found));
    }

    [Test]
    public void ClientNeverFormatsADateWithACultureSensitiveFormat()
    {
        var found = (from line in Lines()
                     from match in DateToString.Matches(line.Text).Cast<Match>()
                     where !RoundTrip.Contains(match.Groups[2].Value)
                     select $"""{line.File}:{line.Number} formats a date as "{match.Groups[2].Value}" - render it through BrowserTime/yap.time instead""")
            .ToArray();
        Assert.That(found, Is.Empty, string.Join(Environment.NewLine, found));
    }

    [Test]
    public void ClientComparesStringsOrdinally()
    {
        var found = (from line in Lines()
                     from match in LinguisticCompare.Matches(line.Text).Cast<Match>()
                     where !match.Groups[2].Value.Contains("StringComparison", StringComparison.Ordinal)
                     select $"{line.File}:{line.Number} calls {match.Groups[1].Value} with no StringComparison - pass StringComparison.Ordinal")
            .ToArray();
        Assert.That(found, Is.Empty, string.Join(Environment.NewLine, found));
    }

    /// <summary>The rewrite above is only *necessary* because of these two flags, and only *safe*
    /// while they stay on: turning them off would hide a regression this guard is meant to catch.</summary>
    [Test]
    public void ClientStillPublishesWithoutIcuOrTimezoneData()
    {
        var project = File.ReadAllText(Path.Combine(PresentationRoot,
            "Presentation/XFramework.Yap.Client/XFramework.Yap.Client.csproj"));
        Assert.Multiple(() =>
        {
            Assert.That(project, Does.Contain("<InvariantGlobalization>true</InvariantGlobalization>"));
            Assert.That(project, Does.Contain("<BlazorEnableTimeZoneSupport>false</BlazorEnableTimeZoneSupport>"));
        });
    }
}
