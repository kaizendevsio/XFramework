namespace Yap.Client;

public static class AppRelease
{
    public const string Version = "1.3.2";
    public static readonly (string Version, string Date, string[] Notes)[] History =
    [
        (Version, "14 September 2026", [
            "Change your profile photo without a permission error.",
            "Video messages show a preview frame before you tap to play.",
            "Conversation times have comfortable spacing at the edge of the inbox."]),
        ("1.3.1", "14 September 2026", [
            "App updates now finish downloading reliably and show the update notification."]),
        ("1.3.0", "14 September 2026", [
            "Protect new messages and attachments with encryption. Approve a new device or use your recovery key to bring your history with you.",
            "Compare identity fingerprints and remove devices you no longer use in Privacy settings.",
            "Talk together in group voice calls while continuing to chat.",
            "Send several photos or videos together, add a profile photo, and give group conversations their own photo.",
            "Watch supported videos inside the conversation and use clearer message and photo action menus.",
            "Long conversations use less memory, with steadier scrolling through older messages and photos."]),
        ("1.2.0", "13 September 2026", [
            "Call someone from your conversation, keep chatting during the call, and mute your microphone when needed.",
            "The message box expands while you write, with easier-to-reach attachment and send buttons.",
            "Favorites have their own section, and selected people stay visible when starting a group conversation.",
            "Message search has clearer results, settings are tidier, and repeated reactions or attachment retries recover automatically."]),
        ("1.1.4", "13 September 2026", [
            "Swipe up or down to close a photo, and tap reader avatars to see who has seen your message.",
            "Conversation settings now have tabs and easy-to-use switches.",
            "Hold a conversation to favorite, rename or remove it. Favorites stay at the top on this device.",
            "A simpler inbox, borderless composer buttons and a compact update notification."]),
        ("1.1.3", "13 September 2026", [
            "Headers now stretch across the screen, with clean glass and a flat background.",
            "Choose your favorite accent color in Settings.",
            "Photos show a loading placeholder and smoothly expand when you open them."]),
        ("1.1.2", "13 September 2026", [
            "Sign-in is preserved through browser recovery and temporary connection problems.",
            "Messages are marked read only while visible in the conversation you're viewing.",
            "Scrolling through older messages stays steady while photos load.",
            "Check your sign-in and connection status in Settings."]),
        ("1.1.1", "13 September 2026", [
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
