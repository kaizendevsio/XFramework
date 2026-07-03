namespace XFramework.Portal.Components.Layout;

public sealed class PortalSidebarState
{
    private readonly HashSet<string> _openGroups = new(StringComparer.OrdinalIgnoreCase);

    public PortalSidebarState(IEnumerable<string> defaultOpenGroups)
    {
        ReplaceOpenGroups(defaultOpenGroups);
    }

    public bool IsCollapsed { get; set; }

    public bool IsHoverExpanded { get; set; }

    public bool IsVisuallyCollapsed => IsCollapsed && !IsHoverExpanded;

    public string? ActiveGroupId { get; set; }

    public IReadOnlyCollection<string> OpenGroupIds => _openGroups;

    public Func<string, Task>? ToggleGroupAsync { get; set; }

    public bool IsGroupOpen(string groupId) => _openGroups.Contains(groupId);

    public bool SetGroupOpen(string groupId, bool open)
    {
        return open
            ? _openGroups.Add(groupId)
            : _openGroups.Remove(groupId);
    }

    public void ReplaceOpenGroups(IEnumerable<string> groupIds)
    {
        _openGroups.Clear();

        foreach (var groupId in groupIds.Where(static groupId => !string.IsNullOrWhiteSpace(groupId)))
        {
            _openGroups.Add(groupId);
        }
    }

    public Task ToggleGroup(string groupId)
    {
        return ToggleGroupAsync?.Invoke(groupId) ?? Task.CompletedTask;
    }
}
