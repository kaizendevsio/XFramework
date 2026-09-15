# Yap browser Bolt messaging

Yap uses a same-origin Bolt WebSocket inside its existing host for message sends,
encrypted message updates, receipts, typing and call notifications. Call media and
attachment streaming retain their separate connections. No new service is required.

The browser obtains a 30-second, single-use ticket using its account header, cookie
and antiforgery token. The ticket binds a server-issued Bolt client identity to the
tenant, account and login session. The dedicated Bolt server accepts local Yap
commands only; it cannot route browser frames to internal services or other clients.
Every command and outgoing event revalidates the login session. Connections renew
before the upstream actor token expires, with a maximum lifetime of five minutes.

Both HTTP compatibility and Bolt sends use `YapChatCommands` and Communications'
existing validation, durable commit, client-message idempotency and outbox wakeup.
Private encryption keys and plaintext remain in the browser. Events are projected
through current tenant, membership, visibility and encryption-audience checks;
raw outbox payloads are never forwarded as browser message data. Remote receipt
events use neutral update metadata; only authorized receipt fields are projected.

The browser verifies, decrypts and saves a pushed message before requesting a
delivery acknowledgment. History catch-up in the new client also suppresses the
legacy fetch-time delivery acknowledgment. Read remains a separate visible-message
action. A server commit, a socket write and an event acknowledgment are not delivery
receipts. Failed sends retain the same persisted message ID and ciphertext; an
uncertain socket write is not immediately repeated over HTTP.

## Recovery and bounds

Each connection numbers events. The browser rejects gaps, deduplicates stable event
IDs and acknowledges only after application. Host and browser queues are limited to
128 events / 8 MiB; overflow closes the connection instead of dropping updates.
Acknowledgment traffic does not block application of newer messages.

Reconnect always starts with explicit history/inbox reconciliation. Periodic
reconciliation remains as repair for missed upstream events. There is no claim of a
persisted browser replay cursor: durable backend history is the recovery source.
The live upstream subscription is transient and independently authenticated per
connection, so multiple tabs do not share a durable subscriber cursor. The existing
Bolt subscription protocol has no readiness acknowledgment; periodic catch-up also
covers the initial subscription window. The legacy SSE endpoint remains temporarily
available for clients that have not updated; new clients do not create EventSource.
An event missed during upstream subscription setup may wait for the 20-second
reconciliation poll. Measure the first message after reconnect separately from
steady-state delivery; this mechanism does not provide gap-free replay.

Sending has its own outbox gate, independent of background network refreshes. Local
message/cache mutations use a short separate gate; HTTP snapshots that began before
a newer push cannot replace it. Reconnection remains quiet and does not claim that
a DNS/server outage means the device has no internet.

Sending rosters are preloaded when opening a conversation and reused for at most
15 seconds in an account-scoped cache of eight entries. Only signature-verified
public directories are cached; simultaneous sends share a pending lookup. Account
and own-key changes, reconnect reconciliation and stale-roster rejection invalidate
the cache. The backend still reads current membership and directory revisions before
every new encrypted message commit. A 412 response forces a fresh roster and one
retry of an unconfirmed message. Calls, edits and receive-side sender verification
continue to fetch their directories normally.

## Validation

Tests cover ticket replay and origin/account/session isolation, forbidden Bolt
routing, receipt privacy and idempotency, concurrent delivery/read acknowledgments,
queue overflow and reconnect, direct local application, duplicate messages, account
switches, stale snapshots and sending during a stalled background request.

The delivery benchmark and rollout evidence live in `artifacts/yap/`. Measure actual
recipient decrypted display separately from server acceptance and delivery receipts.
Desktop Chromium runs do not establish physical Android/iOS performance. Sub-second
delivery is a measurement target for connected foreground clients, not a guarantee
for suspended, offline or reconnecting recipients.
