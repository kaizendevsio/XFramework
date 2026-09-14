namespace Yap.Client;

public static class AppRelease
{
    public const string Version = "1.3.15";
    public static readonly (string Version, string Date, string[] Notes)[] History =
    [
        (Version, "15 September 2026", [
            "Conversations open immediately with the header and composer. Message placeholders appear only when loading takes more than two seconds.",
            "Sending, reactions, edits and conversation preferences update immediately while saving continues. Failed changes are restored without losing your draft."]),
        ("1.3.14", "15 September 2026", [
            "Call audio recovers when Android pauses playback, with a tap-to-resume option when the browser needs your permission.",
            "See who is active in conversation headers. Choose whether to share your own status under Conversation settings → General.",
            "The app now uses ahead-of-time compilation and Brotli-compressed runtime downloads."]),
        ("1.3.13", "15 September 2026", [
            "Only the latest message shows its time in the conversation. Open any message's options to see its full date and time, with date separators through history.",
            "Notifications appear below the header, away from the keyboard and message composer."]),
        ("1.3.12", "15 September 2026", [
            "Encryption and encrypted attachment processing run in a background worker to keep conversations responsive.",
            "Sending appears only after five seconds. Sent and delivered appear on your latest message, and each person's read receipt follows the last message they saw."]),
        ("1.3.11", "14 September 2026", [
            "Keep typing with the keyboard open after sending. New messages appear from the local queue without waiting for the network.",
            "Send a burst of messages smoothly while Yap delivers them in the background, without restarting sync for every message."]),
        ("1.3.10", "14 September 2026", [
            "Conversations open from saved history without waiting for background sync.",
            "Choose a call audio output where your browser supports it, with guidance for devices that use system audio controls.",
            "Connection interruptions show one quiet status instead of repeated errors. Saved chats remain available and queued messages retry automatically.",
            "Attachments saved on your device stay available offline. Other attachments wait for a connection and load when you reconnect."]),
        ("1.3.9", "14 September 2026", [
            "Fixed a Safari issue that damaged encrypted photo and file uploads. New attachments open correctly for you and your recipients.",
            "If an earlier attachment still says unavailable, please send it again after updating."]),
        ("1.3.8", "14 September 2026", [
            "Lost your recovery key and all trusted devices? Start fresh from Privacy settings after confirming your password. Old encrypted messages cannot be recovered with your new keys.",
            "Switch reliably between signing in and creating an account."]),
        ("1.3.7", "14 September 2026", [
            "See message previews and photo or video labels in your inbox while keeping messages encrypted.",
            "Two-person calls end for both people, and ended calls no longer reappear as incoming calls.",
            "The diagnostic logging switch is visible again. Attachment errors now leave more useful troubleshooting logs."]),
        ("1.3.6", "14 September 2026", [
            "Yap waits safely when another tab is using your conversations, then continues automatically when you close it.",
            "Updates and recovery stay available while the app opens. Diagnostic logs now identify which startup step needs attention."]),
        ("1.3.5", "14 September 2026", [
            "Send encrypted messages without waiting for everyone in the conversation to finish setup.",
            "Members who join secure messaging later receive pending messages, photos and videos automatically when your app reconnects."]),
        ("1.3.4", "14 September 2026", [
            "Open saved conversations while Yap reconnects in the background.",
            "Recover from a stuck loading screen and install updates without clearing your saved data."]),
        ("1.3.3", "14 September 2026", [
            "Profile photos display correctly after upload and stay safely attached to your account."]),
        ("1.3.2", "14 September 2026", [
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
