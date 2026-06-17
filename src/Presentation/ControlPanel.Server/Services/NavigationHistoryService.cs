using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace ControlPanel.Server.Services;

public sealed class NavigationHistoryService : IDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly List<string> _history = [];

    public NavigationHistoryService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _history.Add(_navigationManager.Uri);
        _navigationManager.LocationChanged += OnLocationChanged;
    }

    public bool CanGoBack => _history.Count > 1;

    public string? PreviousUri => CanGoBack ? _history[^2] : null;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        var location = args.Location;
        if (_history.Count > 0 && string.Equals(_history[^1], location, StringComparison.Ordinal))
        {
            return;
        }

        var existingIndex = _history.FindLastIndex(x => string.Equals(x, location, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            _history.RemoveRange(existingIndex + 1, _history.Count - existingIndex - 1);
            return;
        }

        _history.Add(location);
    }

    public void Dispose()
    {
        _navigationManager.LocationChanged -= OnLocationChanged;
    }
}
