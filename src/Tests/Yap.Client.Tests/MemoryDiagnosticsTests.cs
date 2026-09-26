using System.Reflection;
using Microsoft.JSInterop;
using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Client.Tests;

public sealed class MemoryDiagnosticsTests
{
    [Test]
    public void Snapshot_ReportsTheManagedHeap_UnderTheNameDiagnosticsJsInvokes()
    {
        var snapshot = MemoryDiagnostics.Snapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.AllocatedBytes, Is.GreaterThan(0));
            Assert.That(snapshot.HeapBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(typeof(MemoryDiagnostics).GetMethod(nameof(MemoryDiagnostics.Snapshot))!.GetCustomAttribute<JSInvokableAttribute>()!.Identifier,
                Is.EqualTo("YapManagedMemory"), "diagnostics.js calls DotNet.invokeMethodAsync with this identifier.");
        });
    }
}
