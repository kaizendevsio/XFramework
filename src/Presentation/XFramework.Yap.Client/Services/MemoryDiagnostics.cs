using Microsoft.JSInterop;

namespace Yap.Client.Services;

/// <summary>
/// Managed-heap numbers for the opt-in diagnostics panel and session-health record. Sizes and
/// counts only. The WebAssembly memory that holds this heap never shrinks, so a managed peak
/// raises the iPhone's WebContent footprint for the rest of the session.
/// </summary>
public static class MemoryDiagnostics
{
    [JSInvokable("YapManagedMemory")]
    public static ManagedMemory Snapshot()
    {
        var info = GC.GetGCMemoryInfo();
        return new(GC.GetTotalMemory(false), info.HeapSizeBytes, GC.CollectionCount(0), GC.CollectionCount(2));
    }
}

public sealed record ManagedMemory(long AllocatedBytes, long HeapBytes, int Gen0Collections, int Gen2Collections);

/// <summary>What <c>yap.diagnostics.health</c> reports; mirrors diagnostics.js, never message data.</summary>
public sealed record SessionHealth(int Navigations, int Transitions, bool? TransitionsEnabled, double? WasmMB, double? JsHeapMB,
    double? ManagedMB, int DomNodes, int GlassLayers, int UptimeS, bool Visible, string? Time = null, bool? Abrupt = null);
