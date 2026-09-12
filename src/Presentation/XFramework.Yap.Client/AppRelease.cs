namespace Yap.Client;

public static class AppRelease
{
    public const string Version = "1.1.1";
    public static readonly (string Version, string Date, string[] Notes)[] History =
    [
        (Version, "13 September 2026", [
            "Turn on diagnostic logging in Settings and copy logs to help troubleshoot a problem, even after a reload.",
            "The photo viewer has a simpler layout. Pinch or double-tap the photo to zoom.",
            "Photo actions are available with a long press, without an extra menu below the photo.",
            "Large photo previews use less memory. Original downloads keep their full quality."]),
        ("1.1.0", "13 September 2026", [
            "Tap a photo to view it full-screen. Pinch, double-tap or use the zoom buttons to see more detail.",
            "Photos appear without a separate filename bubble. Download the original with the arrow on the photo.",
            "See when your messages are sending, delivered and read.",
            "Scroll back through messages without being pulled to the latest one.",
            "Remove a conversation from your own chat list, or delete it for everyone if you're a conversation admin."]),
        ("1.0.1", "12 September 2026", [
            "Large photos finish sending sooner.",
            "Photos from iPhones can be viewed across devices and kept for offline viewing.",
            "App updates are easier to apply without losing saved conversations."])
    ];
}
