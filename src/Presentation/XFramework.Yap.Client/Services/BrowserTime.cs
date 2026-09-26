using Microsoft.JSInterop;

namespace Yap.Client.Services;

/// <summary>
/// Every date and time a person reads is rendered by the browser, not by .NET. The published client
/// ships InvariantGlobalization and no timezone database, which is most of the download saved - but
/// it also means the runtime's local zone silently *is* UTC and its culture is invariant, so formatting
/// on the .NET side would print a plausible, wrong hour and never throw. Intl knows the real zone
/// and the real locale for free. .NET keeps the instants; wwwroot/time.js owns the words.
/// </summary>
public sealed class BrowserTime(IJSRuntime js)
{
    // Blazor WebAssembly's runtime is always in-process, and these are called from render, which
    // cannot await. A DateTime formatted through an awaited call would render one frame late.
    private readonly IJSInProcessRuntime browser = (IJSInProcessRuntime)js;
    // A message window redraws the same forty timestamps on every keystroke, and an absolute
    // rendering of a fixed instant never changes. Relative ones are not cached: they move at midnight.
    private readonly Dictionary<(long Ticks, string Kind), string> cache = [];

    /// <summary>Clock time, in the viewer's zone and their locale's 12/24-hour convention.</summary>
    public string Clock(DateTime utc) => Fixed("clock", utc);

    /// <summary>The viewer's local calendar day as yyyy-MM-dd: a grouping key and a &lt;time datetime&gt; value.</summary>
    public string DayKey(DateTime utc) => Fixed("dayKey", utc);

    /// <summary>Day and month, in the locale's own order.</summary>
    public string DayMonth(DateTime utc) => Fixed("dayMonth", utc);

    /// <summary>Long date plus clock time, for the one place that shows a message's full stamp.</summary>
    public string Full(DateTime utc) => Fixed("full", utc);

    /// <summary>"Today" / "Yesterday" / a long date, for the separators between days of messages.</summary>
    public string DaySeparator(DateTime utc) => Call("daySeparator", utc);

    /// <summary>A search hit's stamp: today is a time, this week a weekday, older a date.</summary>
    public string Stamp(DateTime utc) => Call("stamp", utc);

    /// <summary>"Today" / "Yesterday" / "Earlier": the call history's section headings.</summary>
    public string CallSection(DateTime utc) => Call("callSection", utc);

    /// <summary>A call row's stamp: "Just now", "12 min ago", a clock time today, a day before that.</summary>
    public string Recent(DateTime utc) => Call("recent", utc);

    /// <summary>The machine-readable instant for a &lt;time datetime&gt; attribute. "O" is defined to
    /// ignore the culture, so this one format is still safe to produce here.</summary>
    public static string Iso(DateTime utc) => AsUtc(utc).ToString("O");

    /// <summary>Rows rebuilt from the device cache can come back Kind.Unspecified. Every instant in this
    /// app is UTC, and letting .NET read an Unspecified one as local is exactly the silent shift this
    /// class exists to prevent - so anything doing arithmetic on a stored instant pins the kind here.</summary>
    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(), // culture-ok: normalising a kind, never for display
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private string Fixed(string kind, DateTime utc)
    {
        if (cache.TryGetValue((utc.Ticks, kind), out var text)) return text;
        // Scrolling a long history would otherwise grow this without bound.
        if (cache.Count > 4000) cache.Clear();
        return cache[(utc.Ticks, kind)] = Call(kind, utc);
    }

    private string Call(string kind, DateTime utc) =>
        browser.Invoke<string>($"yap.time.{kind}", (AsUtc(utc) - DateTime.UnixEpoch).TotalMilliseconds);
}
