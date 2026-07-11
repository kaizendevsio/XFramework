using System.Collections.Concurrent;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;

namespace XFramework.Portal.Services;

// Compatibility implementation adapted from BlazorBlueprint 3.12.0 PortalService (Apache-2.0).
public sealed class XfPortalService(ILogger<XfPortalService> logger) : IPortalService
{
    private readonly ConcurrentDictionary<string, PortalRegistration> _portals = new();
    private long _nextOrder;
    private bool _hasWarnedMissingHost;

    public bool HasHost { get; private set; }

    public event Action<PortalCategory>? OnPortalsCategoryChanged;
    public event Action<string>? OnPortalRendered;
    internal event Action<XfPortalChange>? OnPortalChanged;

    public void RegisterHost() => HasHost = true;

    public void UnregisterHost() => HasHost = false;

    public void NotifyPortalRendered(string portalId) =>
        OnPortalRendered?.Invoke(portalId);

    public void RegisterPortal(string id, RenderFragment content, PortalCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(content);

        WarnIfHostIsMissing();

        _portals[id] = new PortalRegistration(
            content,
            category,
            Interlocked.Increment(ref _nextOrder));
        NotifyPortalChanged(id, category);
    }

    public void UnregisterPortal(string id)
    {
        if (_portals.TryRemove(id, out var registration))
        {
            NotifyPortalChanged(id, registration.Category);
        }
    }

    public void UpdatePortalContent(string id, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!_portals.TryGetValue(id, out var registration))
        {
            throw new InvalidOperationException($"Portal with ID '{id}' is not registered.");
        }

        registration.Content = content;
        NotifyPortalChanged(id, registration.Category);
    }

    public void RefreshPortal(string id)
    {
        if (_portals.TryGetValue(id, out var registration))
        {
            NotifyPortalChanged(id, registration.Category);
        }
    }

    public IReadOnlyList<KeyValuePair<string, RenderFragment>> GetPortals(PortalCategory category) =>
        _portals
            .Where(portal => portal.Value.Category == category)
            .OrderBy(portal => portal.Value.Order)
            .Select(portal => new KeyValuePair<string, RenderFragment>(portal.Key, portal.Value.Content))
            .ToList();

    private void NotifyPortalChanged(string id, PortalCategory category)
    {
        OnPortalChanged?.Invoke(new XfPortalChange(id, category));
        OnPortalsCategoryChanged?.Invoke(category);
    }

    private void WarnIfHostIsMissing()
    {
        if (HasHost || _hasWarnedMissingHost)
        {
            return;
        }

        _hasWarnedMissingHost = true;
        logger.LogWarning(
            "No Portal host is registered. Dialog, sheet, popover, and select content cannot render until a host is available.");
    }

    private sealed class PortalRegistration(
        RenderFragment content,
        PortalCategory category,
        long order)
    {
        public RenderFragment Content { get; set; } = content;
        public PortalCategory Category { get; } = category;
        public long Order { get; } = order;
    }
}

internal readonly record struct XfPortalChange(string PortalId, PortalCategory Category);
