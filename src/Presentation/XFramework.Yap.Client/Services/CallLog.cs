using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>Consecutive calls with one conversation, collapsed into a single Recents row.</summary>
public sealed record CallGroup(IReadOnlyList<CallHistoryItem> Calls)
{
    public CallHistoryItem Latest => Calls[0];
    /// <summary>The oldest call anchors the row: a new call joins at the top without changing the key,
    /// so an expanded row stays expanded while history refreshes.</summary>
    public Guid Key => Calls[^1].Message.Id;
    public Guid ThreadId => Latest.Message.ThreadId;
    public CallDirection Direction => CallHistory.Direction(Latest.Message);
    public bool Missed => Direction == CallDirection.Missed;
    public bool Video => CallHistory.IsVideo(Latest.Message);
}

public sealed record CallSection(string Label, IReadOnlyList<CallGroup> Groups);

public static class CallLog
{
    /// <summary>Newest first, split into sections by <paramref name="section"/> (the browser's Today /
    /// Yesterday / Earlier), and within a section every run of calls with the same conversation is one
    /// group. Runs never cross a section header, so a group's calls always share its heading.</summary>
    public static IReadOnlyList<CallSection> Group(IEnumerable<CallHistoryItem> items, Func<DateTime, string> section)
    {
        var sections = new List<(string Label, List<List<CallHistoryItem>> Groups)>();
        // Pages arrive newest first, but a later page can overlap an earlier one; a stable sort keeps
        // the server's order for ties and puts a refreshed first page back above older history.
        foreach (var item in items.DistinctBy(x => x.Message.Id).OrderByDescending(x => BrowserTime.AsUtc(x.Message.CreatedAt)))
        {
            var label = section(item.Message.CreatedAt);
            if (sections.Count == 0 || sections[^1].Label != label) sections.Add((label, []));
            var groups = sections[^1].Groups;
            if (groups.Count > 0 && groups[^1][0].Message.ThreadId == item.Message.ThreadId) groups[^1].Add(item);
            else groups.Add([item]);
        }
        return sections.Select(s => new CallSection(s.Label, s.Groups.Select(g => new CallGroup(g)).ToList())).ToList();
    }
}
