namespace ControlPanel.Server.Components.Shared;

public sealed record XfEntityPickerColumn<TItem>(
    string Title,
    Func<TItem, string?> ValueSelector,
    bool Sortable = true,
    string? CssClass = null);

public sealed record XfEntityPickerFilterOption(
    string Value,
    string Label);

public sealed record XfEntityPickerFilter<TItem>(
    string Key,
    string Label,
    IReadOnlyList<XfEntityPickerFilterOption> Options,
    Func<TItem, string?> ValueSelector,
    string AllLabel = "All");
