---
title: "Yap notifications and call history"
date: 2026-09-16
category: workflow-issues
module: Yap, Communications, Notifications
problem_type: reliability
component: notifications-and-calls
severity: medium
status: current
applies_when:
  - "Maintaining Yap foreground push suppression, call records or microphone consent"
tags: [yap, push, webkit, android, voice, permissions]
---

# Yap notifications and call history (1.3.38)

## Changes

- Each signed-in browser with a push subscription refreshes a foreground lease every 20 seconds. Notifications checks that subscription's lease before sending either message or call pushes. Hiding or leaving clears only that window's lease. Another phone, account or tenant is unaffected. Suppressed delivery jobs complete rather than alerting later.
- Message push urgency is `high`. Replacing a previous notification for the same conversation uses `renotify: true`, allowing another alert on supporting Android browsers. Calls keep their existing short expiry and actions.
- Yap's call gateway records one conversation entry when a room ends or an unanswered invitation expires. The call ID deduplicates retries and concurrent hang-up/disconnect paths. Encrypted group calls start their duration when at least two participants are ready. A call entry contains server-observed timing and outcome only, never audio or encrypted message contents. Normal message encryption remains required.
- Communications owns persistence and realtime fan-out. Only the authenticated Yap service can record a call; browser actors cannot. Call entries cannot be edited and do not send a second message push. They remain in conversation history and its offline cache.
- Call settings offers an explicit microphone check, reports denied/unavailable access, and immediately releases test capture. Mute disables transmission and the existing audio track; unmute reuses capture. Hang-up still stops the tracks. There is no manifest permission bypass or permanent permission promise.

## Operational limits

Foreground suppression uses a short in-memory lease in the current single-instance Notifications deployment. Before scaling Notifications to multiple instances, move this registry to shared storage. A crashed window's lease expires after 45 seconds. A notification already dispatched when the app opens can still arrive: a received Web Push must remain user-visible, especially on WebKit. The worker does not silently discard it.

Completed call writes retry through the gateway's cleanup loop during a temporary Communications outage. The gateway and pending retries remain in memory, like existing active calls: an abrupt gateway process loss before persistence can lose an unfinished/pending call record. Stored entries survive restarts. This release does not backfill calls made before these records existed.

Deploy Communications, Notifications and Yap together because the internal contracts changed. Physical Android notification presentation still requires device acceptance testing; provider acceptance and automated worker tests do not establish that an OS displayed a banner. iOS retains control of microphone consent across sessions and installations.

## Verification

- Notifications tests: subscription ownership, per-device/window/tenant isolation, lease expiry, foreground queue completion, priority, and existing push authorization/crypto/delivery checks.
- Communications tests: missed/completed records, duration, retry deduplication, rejection of browser-forged records, preservation of encrypted-thread policy, and no second push for call history.
- Yap tests: authenticated/antiforgery-protected presence endpoint, hang-up history, retry after an unavailable history service, and existing call lifecycle regressions.
- Browser script tests: both service workers, account/visibility transitions, quiet offline failures, microphone-check cleanup, and one microphone capture across mute/unmute with release on hang-up.

Device acceptance: with two signed-in phones, leave one foreground and the other background; send a message and place a call. Check that only the background phone alerts, consecutive messages can alert, and both receive normal background alerts after switching away. Complete and miss calls, reload the conversation, and check duration/outcome entries. On iPhone, check permission guidance, mute/unmute without another capture request, and microphone release on hang-up.
