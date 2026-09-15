using System.Text;

namespace Yap.Client.Services;

/// <summary>One run of a search result's excerpt. <see cref="Match"/> marks the part the query hit.</summary>
public readonly record struct SearchSegment(string Text, bool Match);

/// <summary>Turns a matched message into the short, match-centred excerpt a result row shows.
/// A row is two lines tall, so a long message has to be cut down to the part that explains the match.</summary>
public static class SearchSnippet
{
    /// <summary>Characters kept before the match so it still reads as a sentence.</summary>
    public const int Lead = 24;
    /// <summary>Excerpt budget: roughly the two lines a result row can show at phone width.</summary>
    public const int Budget = 110;
    private const string Ellipsis = "…";

    public static List<SearchSegment> Build(string? text, string? query, int budget = Budget)
    {
        var flat = Flatten(text);
        var needle = (query ?? "").Trim();
        if (flat.Length == 0) return [];
        var first = needle.Length == 0 ? -1 : flat.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        // No match still has to render: the local scan matches on raw text the excerpt has collapsed.
        if (first < 0)
        {
            var head = Clip(flat, budget, out var clipped);
            return clipped ? [new(head, false), new(Ellipsis, false)] : [new(head, false)];
        }

        // A message that already fits is shown whole; trimming its head would only lose context.
        var start = flat.Length <= budget ? 0 : Align(flat, Math.Max(0, first - Lead));
        // The window always covers the whole first match, however long the query is.
        var end = Align(flat, Math.Min(flat.Length, Math.Max(start + budget, first + needle.Length)));
        var window = flat[start..end];
        List<SearchSegment> segments = [];
        if (start > 0) segments.Add(new(Ellipsis, false));
        for (var at = 0; at < window.Length;)
        {
            var hit = window.IndexOf(needle, at, StringComparison.OrdinalIgnoreCase);
            if (hit < 0) { segments.Add(new(window[at..], false)); break; }
            if (hit > at) segments.Add(new(window[at..hit], false));
            segments.Add(new(window.Substring(hit, needle.Length), true));
            at = hit + needle.Length;
        }
        if (end < flat.Length) segments.Add(new(Ellipsis, false));
        return segments;
    }

    /// <summary>The excerpt as plain text, for titles and accessible labels.</summary>
    public static string Plain(IEnumerable<SearchSegment> segments) => string.Concat(segments.Select(x => x.Text));

    /// <summary>Message text is multi-line; a result row is not. Collapse runs of whitespace to single spaces.</summary>
    private static string Flatten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character)) { space = builder.Length > 0; continue; }
            if (space) { builder.Append(' '); space = false; }
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static string Clip(string text, int budget, out bool clipped)
    {
        var end = Align(text, Math.Min(text.Length, budget));
        clipped = end < text.Length;
        return text[..end];
    }

    /// <summary>Never cut between a surrogate pair: half an emoji renders as a replacement box.</summary>
    private static int Align(string text, int index) =>
        index > 0 && index < text.Length && char.IsLowSurrogate(text[index]) ? index - 1 : index;
}
