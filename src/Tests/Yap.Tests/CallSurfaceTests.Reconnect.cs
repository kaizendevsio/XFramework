using System.Reflection;
using Bolt.Media;
using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Tests;

#pragma warning disable BL0006

public sealed partial class CallSurfaceTests
{
    /// <summary>
    /// Reconnecting, recovering, failing and calling back all happen inside the one dialog. A second
    /// dialog anywhere in that sequence would be a closed one: the call screen would vanish behind the
    /// chat exactly when the person most needs to see what is going on.
    /// </summary>
    [Test]
    public async Task ReconnectingFailingAndCallingBack_AllHappenInTheOneDialog()
    {
        await using var call = new SurfaceFixture();
        await call.RenderAsync();
        var surface = call.Opened.Single();
        var link = new CallLinkMonitor();
        link.Connected(0);
        await call.ChangeAsync(() =>
        {
            Set(call.CurrentAttempt, "Link", link);
            Set(call.CurrentAttempt, "EverConnected", true);
        });

        // Connected → Reconnecting → Connected, with the camera on for good measure.
        await call.SetVideoAsync(true);
        await call.ChangeAsync(() => { link.Lost(1); Set(call.CurrentAttempt, "ReconnectingSince", (DateTimeOffset?)DateTimeOffset.UtcNow); });
        Assert.That(call.Has("canvas"), Is.True, "the picture stays up while reconnecting");
        await call.ChangeAsync(() => link.Resumed(2));
        await call.SetVideoAsync(false);

        // Failed: the attempt is gone, the ended screen takes the same dialog.
        await call.ChangeAsync(() =>
        {
            typeof(VoiceState).GetProperty(nameof(VoiceState.Ended))!.SetValue(call.Voice,
                new CallEndedNotice("Call ended", "connection lost", Guid.NewGuid(), "Alex", null, false));
            typeof(VoiceState).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(call.Voice, null);
        });
        Assert.That(call.Label, Is.Null, "the dialog now says the call ended rather than that it is on");
        Assert.That(call.Labelled("Call back"), Is.EqualTo(1));

        // Call back: a new attempt appears and the ended screen gives way in place.
        await call.ChangeAsync(() =>
        {
            typeof(VoiceState).GetProperty(nameof(VoiceState.Ended))!.SetValue(call.Voice, null);
            typeof(VoiceState).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(call.Voice, call.CurrentAttempt);
        });

        Assert.Multiple(() =>
        {
            Assert.That(call.Inserted("dialog"), Is.EqualTo(1), "one dialog node from the first ring to the call back");
            Assert.That(call.Opened, Is.EqualTo(new[] { surface }), "and it was only ever opened once");
            Assert.That(call.Label, Is.EqualTo("Voice call"));
        });
    }

    private static void Set(object target, string field, object? value) => target.GetType().GetField(field)!.SetValue(target, value);
}
#pragma warning restore BL0006
