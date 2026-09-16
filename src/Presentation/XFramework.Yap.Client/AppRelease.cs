namespace Yap.Client;

public static class AppRelease
{
    public const string Version = "1.3.36";
    public static readonly (string Version, string Date, string[] Notes)[] History =
    [
        (Version, "16 September 2026", [
            "Your chosen ringtone now plays only when you receive a call; placing one plays a standard ringback tone.",
            "Incoming calls ring and buzz more reliably, and a tap anywhere starts the ring if the browser blocked it."]),
        ("1.3.35", "16 September 2026", [
            "An incoming call notification now has Open call and Dismiss buttons, and buzzes on phones that support it.",
            "A call notification that arrives too late shows as a missed call instead of a call you cannot answer."]),
        ("1.3.34", "15 September 2026", [
            "Message search is rebuilt: results show who said it and highlight the match in context.",
            "Searching a conversation now opens its own screen instead of a cramped sheet."]),
        ("1.3.33", "15 September 2026", [
            "Saved messages now have their own tab, with everything you have bookmarked across conversations."]),
        ("1.3.32", "15 September 2026", [
            "Yap can now notify you about new messages and incoming calls while the app is closed or in the background.",
            "Turn notifications on in Settings. On iPhone, add Yap to your Home Screen first."]),
        ("1.3.31", "15 September 2026", [
            "Calls now ring out loud: a ringback while you wait and a ringtone when someone calls you.",
            "Choose your ringtone and ring volume under Settings, Calls."]),
        ("1.3.30", "15 September 2026", [
            "Delivered now appears as soon as the other device receives and saves your message, not when they open the conversation."]),
        ("1.3.29", "15 September 2026", [
            "Messages you send and receive now animate into the conversation.",
            "The message list rubber-bands when you reach either end.",
            "Tapping beside a message's action menu now closes it.",
            "Edit only appears while the message can still be edited."]),
        ("1.3.28", "15 September 2026", [
            "Voice messages have a proper player with a waveform you can scrub, and received clips play in the conversation.",
            "Recording shows a live level meter and a running clock.",
            "Videos open full screen in the same viewer as photos.",
            "Opening a photo or video hides its thumbnail, so it no longer shows through while you swipe to close."]),
        ("1.3.27", "15 September 2026", [
            "Settings uses one consistent edge spacing on every page instead of drifting per section.",
            "Your chosen accent colour now applies across the app, and the chrome no longer carries a green tint.",
            "The sign-in status line under your photo is gone; connection notices appear as a toast."]),
        ("1.3.26", "15 September 2026", [
            "Messages sent inside a thread stay in that thread instead of also appearing in the conversation.",
            "The replies badge, conversation preview and unread count no longer count thread messages."]),
        ("1.3.25", "15 September 2026", [
            "Your own typing updates no longer compete with incoming messages."]),
        ("1.3.24", "15 September 2026", [
            "Live message updates share queued lookups to reduce delays when both people send rapidly."]),
        ("1.3.23", "15 September 2026", [
            "Brief server reconnects keep messages queued and retry automatically."]),
        ("1.3.22", "15 September 2026", [
            "Verified recipient keys are prepared when opening a conversation so rapid sends avoid repeated setup requests.",
            "Live updates can finish reconnecting while a large send queue is still draining."]),
        ("1.3.21", "15 September 2026", [
            "Encrypted messages arrive directly over a persistent connection, with sending independent of background refreshes.",
            "Delivery receipts confirm that the receiving device has decrypted and saved the message.",
            "Reconnecting quietly restores missed updates while keeping queued messages safe."]),
        ("1.3.20", "15 September 2026", [
            "New messages and receipts update directly without reloading the whole conversation and inbox.",
            "Live updates reconnect after a server interruption without needing to reopen the conversation."]),
        ("1.3.19", "15 September 2026", [
            "Queued messages get a turn before inbox refreshes, and conversation reads run together to reduce delivery delay.",
            "A brief server reconnect retries a read once instead of immediately waiting for the next background sync."]),
        ("1.3.18", "15 September 2026", [
            "Going back from chat details immediately restores the conversation, including its draft and scroll position.",
            "Connection notices now say when Yap cannot be reached instead of claiming your internet is offline."]),
        ("1.3.17", "15 September 2026", [
            "Full-page conversation details, new messages and grouped settings make navigation simpler. Tap a conversation photo to open its details.",
            "Solid message bubbles, smoother read receipts, popover exits and photo swipes reduce visual work while scrolling.",
            "Profile photos stay cached, conversation headers show active or last-seen status, and Camera and Record video use separate capture options.",
            "Startup keeps one consistent splash, with status text changing as saved conversations load."]),
        ("1.3.16", "15 September 2026", [
            "Applying an app update waits for every message in a rapid send burst to finish saving on your device."]),
        ("1.3.15", "15 September 2026", [
            "Conversations open immediately with the header and composer. Message placeholders appear only when loading takes more than two seconds.",
            "Sending, reactions, edits and conversation preferences update immediately while saving continues. Failed changes are restored without losing your draft."]),
        ("1.3.14", "15 September 2026", [
            "Call audio recovers when Android pauses playback, with a tap-to-resume option when the browser needs your permission.",
            "See who is active in conversation headers. Choose whether to share your own status under Conversation settings ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¾ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ General.",
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
