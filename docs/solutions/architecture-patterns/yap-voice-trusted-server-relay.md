# Yap voice: authenticated trusted-server relay

Decision date: 2026-09-13. Scope: the Yap voice slice; this does not release the experimental Bolt Hub media feature from quarantine.

## Security and transport decision

Yap uses a dedicated Bolt media instance inside its existing web host. Browsers connect to the same origin over WSS and authenticate with their existing Yap session cookie. This is **transport-encrypted, trusted-server audio**, not end-to-end encryption: the relay can access encoded audio. The experimental ECDH path remains disabled and must not silently fall back to plaintext.

This avoids exposing the private Bolt Hub or handing browser clients its service credentials. The existing Hub `/bolt/ws` policy requires a dedicated service transport JWT and `bolt.service` scope; registering every user with Yap's shared service identity would incorrectly collapse identities and quotas. The public Yap Funnel on port 8443 can carry this same-origin WSS route. No TURN server, UDP port, public Hub route, or networking change is introduced by the gateway. TCP head-of-line blocking remains a limitation to measure on mobile networks.

`Yap:Calls:Enabled` defaults to false. Enabling it additionally requires the explicit value `Yap:Calls:SecurityMode=TrustedServerTls`. The shared Hub's `BoltConfiguration:MediaEnabled` stays false. This decision is specific to the WSS audio slice and is not permission to advertise E2EE, video, group conferencing, QUIC, P2P, FEC, or congestion-control parity.

## Connection and invitation contract

All POST routes require the normal Yap account header, cookie authentication, and antiforgery token.

1. `POST /api/chat/calls/` accepts `StartYapCall { threadId, recipientId }`. Both must be current members of the conversation. The server derives the tenant, caller identity and display name, then issues a 60-second invitation. The recipient receives an `incoming` call event through the authenticated app SSE connection, even outside that conversation.
2. Each party calls `POST /api/chat/calls/{id}/connect`. The response contains `{ callId, url, clientId, recipientClientId }`. The URL has a random, 30-second, single-use ticket bound to the exact call, tenant, credential and login session. No account access token reaches the browser.
3. Upgrade `/api/chat/calls/socket?ticket=...` accepts HTTP/1.1 GET and HTTP/2 extended CONNECT. Mapping only GET breaks browsers that use HTTP/2 WebSockets. Both paths require HTTPS, the exact HTTPS Origin and the same authenticated cookie session. A different account, replay, insecure request or expired invitation is rejected. Membership is rechecked immediately before upgrade.
4. The dedicated Bolt server requires the host-attested secure transport, authenticated principal and exact server-assigned registration identity. It accepts only media protocol traffic, not RPC, pub/sub, arbitrary streams or batches.
5. After Bolt registration and browser handler initialization, `POST /api/chat/calls/{id}/ready` emits `ready`. The caller starts the matching server-issued call ID only after the recipient is ready. The receiver answers with media handlers already installed.
6. `POST /api/chat/calls/{id}/end` and websocket loss cancel the invitation and both connection lifetimes. The server limits active calls and queues; ringing invitations expire and active sockets have a one-hour maximum lifetime. Media membership is rechecked by the dedicated Bolt server on its authorization lease.

The first slice is two-party audio. A conversation with more members can supply one selected recipient, but this must be labelled an individual call, not a group conference. `GET /api/chat/calls/config` reports this explicitly with `groupCalls: false`.

The gateway stores transient call state in memory. The supported deployment is one Yap instance. A restart ends active calls, but does not revoke saved Yap login sessions. Multi-instance calls need a separate routing/state design before increasing replicas.

## Acceptance evidence and remaining gates

`BoltMediaAuthorizationTests` exercises actual Bolt frame handling with authenticated principals: deny without a policy, anonymous caller denial, tenant-policy denial, policy exception, membership revocation before answer, principal/global quotas and release, participant bound, and exact Opus packet forwarding stopped after hangup. These tests prove authorization and framed routing, not microphone capture or decoded playback.

`YapCallGatewayTests` checks explicit security-mode enablement, exact Origin, invitation recipient isolation, tenant/nonmember denial, membership removal before joining, HTTPS enforcement, one-use session-bound tickets and readiness ordering. Failure of the network upgrade must not make a consumed ticket reusable.

`YapCallEndpointTests` runs the real Yap HTTP pipeline on HTTPS with two independently authenticated cookie containers and the normal antiforgery/account headers. Separate cases require HTTP/1.1 and HTTP/2 exactly. It verifies incoming invitations outside a selected conversation and ready-state replay on SSE resubscription, upgrades both WSS connections, registers the server-issued identities, negotiates a call, forwards an actual Opus silence packet through the gateway and observes hangup. The portable CI fixture pins its exact ephemeral localhost certificate and rejects a hostname mismatch; it does not accept arbitrary certificates. This proves the complete host/protocol route; it does not prove microphone capture or audible playback.

The isolated browser fixture supports `YAP_FIXTURE_CALLS=1` and HTTPS. Use `https://alice.dev.localhost:<port>` with `fixture` / `fixture` and `https://bob.dev.localhost:<port>` with `callee` / `fixture` to keep cookies independent while sharing one relay and conversation. These mock identities and the calls flag exist only in the test project. Both names match the development certificate's `*.dev.localhost` SAN. Do not copy test credentials, mock membership or certificate bypasses into runtime configuration.

### Browser verification, 13 September 2026

The trimmed Release WebAssembly app was tested through the Chrome extension with two independently authenticated HTTPS browser tabs. An oscillator-backed `MediaStream` replaced the physical microphone to avoid recording the room; playback passed through an analyser with physical speaker output muted. The AudioWorklet, JS/.NET interop, Opus codecs, authenticated gateway and remote audio rendering were real. This verifies the browser audio pipeline, not physical microphone permissions or device acoustics.

Native/native and native/managed-Opus calls both exchanged thousands of packets and rendered the other peer's distinct tone. The managed peer had `AudioEncoder` unavailable, proving the fallback rather than silently using native codecs. Calls were exercised for over two minutes. After the preparation fix, ringing produced zero encoded frames; received RMS remained about 0.071–0.073 for a 0.1-amplitude sine wave, including after mute/unmute. Playback sources stayed bounded (observed maximum six), and ended calls left zero live tracks, zero active sources and closed audio contexts/codecs.

Incoming calls appeared while the recipient was in the inbox. Mute stopped transmitted packets while reception continued. Minimize/restore, navigation to the inbox during a call, repeated calls, decline and the unanswered-call timeout were exercised. Automated coverage additionally verifies account changes, cancellation during microphone preparation, readiness replay, membership revocation and both HTTP/1.1 `GET` and HTTP/2 `CONNECT` upgrades.

The Docker Yap configuration explicitly enables this two-party trusted-server mode. Base configuration remains disabled. Shared Hub media and ECDH remain disabled. Public deployment verification must check the existing Funnel WSS path and retain HTTPS, exact Origin, session and membership enforcement.

### Remaining device limits

Physical Android/iOS microphone permission prompts, mobile autoplay behavior, device routing and slow/cellular-network quality still require device testing. Desktop tests do not establish mobile parity. Group conferencing, video, E2EE, background native call notifications and automatic active-call recovery after a server restart are not supported by this slice.

### Gated group lifecycle groundwork, 14 September 2026

`BoltServer.Groups.cs` provides host-managed admission for up to eight accepted, authenticated participants. This does not enable the legacy `AddParticipant` path or supply encryption. Admission requires a separate `IBoltGroupCallAuthorizer`; a missing policy denies it. New participants receive existing codec configurations before live media routing. Every active participant is checked on the authorization lease, including the current sender before a frame is forwarded. Each participant's End signal and transport loss remove only that member and its streams. Server-originated `StreamEnded` (`0x0D`, exactly one 16-byte stream ID) allows other clients to release the corresponding decoder without ending the room.

`YapCallGateway.Groups.cs` separates invitation, explicit acceptance, transport registration and readiness. Acceptance and tickets bind to the accepting login session. Leave invalidates that participant's tickets, cancels its connection and preserves other accepted members. The production gateway constructor disables group admission; group lifecycle methods are internal and no group endpoints are mapped. The test constructor exercises this unfinished integration without a deployable configuration override. Configuration still reports `groupCalls: false`.

`BoltGroupCallLifecycleTests` uses real Bolt frame processing to verify admission gates, no audio to unaccepted invitees, late configuration ordering, individual leave/disconnect, sender and third-member revocation, and capacity/call binding. Additional `YapCallGatewayTests` cover acceptance, session/tenant/membership checks, readiness and failed-upgrade isolation. These are lifecycle and transport tests, not encrypted group audio or multi-browser playback evidence. Verified shared device identity, E2EE key management and membership key rotation remain required before group UI/API enablement.

### Resumable calls, 27 September 2026

A lost call socket no longer ends an encrypted group call. The gateway holds the seat for `Yap:Calls:ReconnectGraceSeconds` (default 45 s) and the roster marks the participant `Reconnecting`; the revision, and so the SFrame epoch, is unchanged. The relay reports why a participant left (`GroupParticipantDeparted`: `Left`, `Disconnected`, `Unauthorized`): only a closed socket is held, a refusal ends the seat at once. When a hold runs out the participant leaves as before, and a two-person call ends with `group-ended` reason `connection-lost`.

`POST /api/chat/calls/groups/{id}/resume { deviceId }` issues a resume ticket after re-checking membership and device (a refusal ends the seat; an unavailable backend is 503 and the seat stays held). The ticket is 256 random bits, single use, 30 s, stored only as a hash, and bound to the call, account, sign-in session, device and the seat generation it replaces; reissuing rotates it, and a completed resume, leaving or the call ending voids it. The join ticket is never reused. A resume supersedes a socket the server still believes is alive (an IP change): the old connection finishes leaving the relay before the new one registers, and each socket's principal carries its generation (`yap_connection`) so the relay's refusal of a superseded connection is not the participant leaving. Call sockets use WebSocket keep-alive pings (`Yap:Calls:KeepAliveTimeoutSeconds`, 20 s) so a dead TCP is noticed server-side.

The client keeps the microphone, camera, SFrame session and epoch through the swap, so a resume within the same roster needs no rekey and reuses no nonce (the SFrame sender counter simply continues; receivers' replay windows only move forward). If the roster changed while away, the device joins the new epoch the normal way. Liveness uses `SignalType.Heartbeat` (`0x0E`), which the relay echoes to the sender alone. Timing is RTT-scaled (`CallLinkMonitor`, `CallReconnector` in Bolt.Media). The one-hour call cap is now `Yap:Calls:MaxCallHours` (default 12): it bounded abandoned rooms and a socket authenticated once, and the relay's 5 s re-authorization now covers the second. `Bolt.CallResilience.Harness` measures resumes over netem-shaped TCP (`RESUME=1`).
