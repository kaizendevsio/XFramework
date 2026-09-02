namespace XFramework.Portal.Shared.Components;

public class EditableFormContext
{
    public bool IsEditing { get; set; }
    public bool IsDirty { get; set; }

    private readonly HashSet<string> _dirtyFields = [];

    public event Action? Changed;

    public void MarkFieldDirty(string fieldId)
    {
        var wasDirty = IsDirty;
        _dirtyFields.Add(fieldId);
        IsDirty = _dirtyFields.Count > 0;
        NotifyIfChanged(wasDirty);
    }

    public void MarkFieldClean(string fieldId)
    {
        var wasDirty = IsDirty;
        _dirtyFields.Remove(fieldId);
        IsDirty = _dirtyFields.Count > 0;
        NotifyIfChanged(wasDirty);
    }

    public void Reset()
    {
        var wasDirty = IsDirty;
        _dirtyFields.Clear();
        IsDirty = false;
        IsEditing = false;
        NotifyIfChanged(wasDirty);
    }

    private void NotifyIfChanged(bool wasDirty)
    {
        if (wasDirty != IsDirty)
        {
            Changed?.Invoke();
        }
    }
}
