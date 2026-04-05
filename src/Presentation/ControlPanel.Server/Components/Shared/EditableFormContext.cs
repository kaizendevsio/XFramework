namespace ControlPanel.Server.Components.Shared;

public class EditableFormContext
{
    public bool IsEditing { get; set; }
    public bool IsDirty { get; set; }

    private readonly HashSet<string> _dirtyFields = [];

    public void MarkFieldDirty(string fieldId)
    {
        _dirtyFields.Add(fieldId);
        IsDirty = _dirtyFields.Count > 0;
    }

    public void MarkFieldClean(string fieldId)
    {
        _dirtyFields.Remove(fieldId);
        IsDirty = _dirtyFields.Count > 0;
    }

    public void Reset()
    {
        _dirtyFields.Clear();
        IsDirty = false;
        IsEditing = false;
    }
}
