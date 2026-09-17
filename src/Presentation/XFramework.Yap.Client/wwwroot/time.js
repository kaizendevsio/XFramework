// Every date and time a person reads is rendered here, not in .NET. The published runtime ships
// InvariantGlobalization with no timezone database, so in WASM TimeZoneInfo.Local *is* UTC and
// CurrentCulture is invariant: formatting there would silently print the wrong hour and nothing
// would throw. Intl already knows the real zone and the real locale, and costs zero download.
// .NET keeps the instants (UTC) and passes epoch milliseconds; the browser owns the words.
(() => {
    // Intl.DateTimeFormat construction is the expensive part; the formatters themselves are reusable.
    const cache = new Map();
    const formatter = (key, options, locale) => {
        let made = cache.get(key);
        if (!made) cache.set(key, made = new Intl.DateTimeFormat(locale, options));
        return made;
    };
    // Sortable local calendar day. Pinned to en-CA/gregory/latn because this string is an identity
    // used for grouping and for the <time datetime> attribute, not something a person reads; an
    // Islamic or Persian default calendar would still group correctly but would not be a valid date.
    const dayKey = ms => formatter('key', { calendar: 'gregory', numberingSystem: 'latn', year: 'numeric', month: '2-digit', day: '2-digit' }, 'en-CA').format(ms);
    // Whole days apart in the *viewer's* calendar, which is the only definition of "yesterday" that
    // survives DST and a midnight-crossing offset. Differencing the epoch would not.
    const daysAgo = ms => {
        const local = new Date(dayKey(ms) + 'T00:00:00Z'), today = new Date(dayKey(Date.now()) + 'T00:00:00Z');
        return Math.round((today - local) / 86400000);
    };
    const clock = ms => formatter('clock', { hour: 'numeric', minute: '2-digit' }).format(ms);
    const weekday = ms => formatter('weekday', { weekday: 'short' }).format(ms);
    const dayMonth = ms => formatter('dayMonth', { month: 'short', day: 'numeric' }).format(ms);
    const monthYear = ms => formatter('monthYear', { month: 'short', year: 'numeric' }).format(ms);
    const longDate = ms => formatter('longDate', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }).format(ms);
    const full = ms => formatter('full', { dateStyle: 'long', timeStyle: 'short' }).format(ms);
    // "Today"/"Yesterday" stay English on purpose: every other string in this app is English, and a
    // half-translated screen reads worse than a consistent one. What had to move to the browser is
    // the zone and the locale's own conventions (12h vs 24h, day/month order), not the vocabulary.
    window.yap.time = {
        clock, dayKey, dayMonth, full,
        daySeparator(ms) { const days = daysAgo(ms); return days === 0 ? 'Today' : days === 1 ? 'Yesterday' : longDate(ms); },
        // Dates read as a person would say them; a same-day hit only needs a clock time.
        stamp(ms) {
            const days = daysAgo(ms);
            if (days === 0) return clock(ms);
            if (days === 1) return 'Yesterday';
            if (days < 7) return weekday(ms);
            return new Date(ms).getFullYear() === new Date().getFullYear() ? dayMonth(ms) : monthYear(ms);
        }
    };
})();
