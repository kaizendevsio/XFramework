# Yap encrypted recovery: confirmed experience and pending backend decision

Status: recovery experience confirmed by the user on 14 September 2026; encryption implementation and backend choice remain pending. Current Yap messages and calls must not be described as end-to-end encrypted.

## Confirmed experience

- Encrypt new messages by default, including group conversations and attachment content.
- Keep encrypted history and encrypted recovery material on the server so a replacement device can recover history.
- A new device gains decryption access through approval from an existing trusted device or the user's recovery key.
- Losing every trusted device and the recovery key makes previous encrypted history unrecoverable. Account login or password reset alone must not decrypt history.
- Recovery must create a fresh device identity and sending state; never clone active sender counters/ratchets onto two devices.
- Calls, including group calls, need participant-device encryption. TLS to the Bolt relay does not satisfy that requirement.

## Proposed backend boundary, awaiting a decision

Recommend evaluating a self-hosted Matrix homeserver and its maintained JavaScript SDK behind the existing Yap interface and account experience. The SDK supports browsers and includes a Rust-based encryption implementation; Matrix documents device cross-signing and encrypted key backup. This is an integration proposal, not a claim of turnkey compatibility with Yap or Bolt. Sources: [Matrix JavaScript SDK](https://github.com/matrix-org/matrix-js-sdk), [Matrix cross-signing and recovery guidance](https://matrix.org/docs/older/e2ee-cross-signing/).

The proposal changes ownership of encrypted message delivery, device keys and recovery storage. XFramework would retain application identity and the Yap UI, with an explicit mapping between conversations/users and the encrypted service. No user should need a separate chat app. Authentication integration, membership authority, encrypted media, local search, retention and old plaintext-history migration must be settled before rollout. Old server-visible messages cannot retroactively acquire an E2EE confidentiality guarantee.

An external homeserver is an architecture change under `rules/BackendGuidelines.md`: XFramework currently uses one physical database with module-owned schemas. Do not add a homeserver/database or replace Communications storage silently. The alternative is retaining Communications and integrating a reviewed group-encryption library; that needs substantially more device identity, persistence, recovery and protocol integration work.

Bolt remains the requested media transport. Its group lifecycle and independent Opus decoders are prepared but gated. Connect media key distribution to the selected verified-device system, use a reviewed frame-encryption implementation, and verify membership rotation, tamper/replay rejection and three-device audio before exposing group calls. Do not enable the legacy experimental ECDH implementation.

## Completed local work independent of that decision

- Profile photo upload through an authenticated self-profile endpoint; callers cannot choose another credential ID.
- Group photo upload restricted to group admins, with authorized Storage references and membership-checked downloads. A nullable conversation photo ID migration is included.
- Inbox section padding; message action popup with blurred backdrop, reactions and selected-message/photo preview.
- MOV/MP4/WebM detection when mobile file pickers omit MIME types; inline native video range streaming. Unsupported codecs offer a download fallback. Cross-browser transcoding is not implemented.
- A maximum of 100 hydrated messages per active history/reply window and at most 80 rendered rows. Older/newer paging, cached search jumps, reply refresh anchors and leaving-conversation memory cleanup are covered.
- 128 kbps Opus encoder target and independent bounded participant playback; gated group admission, routing, departure and cleanup.

## Verification as of 14 September 2026

- Yap host: 89 tests passed; Yap client: 35 tests passed.
- Communications service security: 58 tests passed, including group photo authorization. Two pre-existing PostgreSQL/Testcontainers cases could not start because local Docker was unavailable.
- Identity self-profile authorization: 2 tests passed.
- Browser audio: 17 JavaScript tests; Bolt focused security/lifecycle: 36 tests passed. The host total includes 22 gateway cases.
- Scroll/image JavaScript: 13 tests passed, including reverse history navigation and video URL/account binding.
- Chrome extension, 390 x 844 viewport, isolated disposable fixture: profile and group photos uploaded and displayed as 512 x 512 images; a generated H.264 MOV played inline and advanced its playback clock; rapid upward scroll exercised a 600-message text/image history with bounded DOM rows; text/photo context menus inspected visually.
- This is desktop Chrome mobile-sized verification, not physical iOS/Android testing, three-device encrypted voice, or a deployment claim.

Local test outputs are under `artifacts/yap/`. Detailed crypto/group readiness research is in `artifacts/yap-e2ee-group-voice-readiness.md`; it is an ignored local artifact. The tracked [Bolt voice implementation notes](yap-voice-trusted-server-relay.md) explain the production gates.
