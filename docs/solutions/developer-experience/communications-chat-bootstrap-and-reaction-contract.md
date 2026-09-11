---
title: "Communications chat bootstrap and reaction contracts"
date: 2026-09-11
category: developer-experience
module: Communications
status: current
---

# Chat bootstrap and reads

Use `ICommunicationsChatClient.ForCurrentActorAsync()`. The actor token and effective
tenant are authoritative; never supply another user's credential to impersonate them.

Before first use, call `session.EnsureChatDefaultsAsync(ct)`. This explicit command
requires `communications.chat:create` plus the caller service's `communications.chat`
scope. It creates only fixed Communications-owned defaults in the authenticated tenant:
a Chat message type, Chat thread type, six reaction types, and Delivered/Read delivery
types. It does not create permissions, roles, or Identity rows. PostgreSQL transaction
locking makes concurrent first-use calls idempotent. Existing active types are reused.

Message/delivery primary keys belong to each tenant. Constants such as `MessageTypes.Chat`
are semantic `SystemReferenceId` values, not primary keys to copy into every tenant.
Existing tenant-owned legacy records whose primary key equals the constant remain readable.

| SDK call | HTTP route | Result data |
| --- | --- | --- |
| EnsureChatDefaultsAsync | POST /api/communications/chat/defaults | ChatReferenceDataResponse |
| GetChatReferenceDataAsync | GET /api/communications/chat/reference-data | ChatReferenceDataResponse (read-only) |
| GetReactionsAsync | GET /api/communications/threads/{threadId}/messages/{messageId}/reactions | PaginatedResult&lt;MessageReactionResponse&gt; |
| GetRepliesAsync | Existing message-history route with ParentMessageId | GetThreadMessagesResponse |

The new read endpoints require `communications.chat:view` and the CommunicationsChat
service scope. The reaction read validates active membership and the existing hidden/
blocked-message rules. Page size is 1–100; page index is 0–100.

`ChatReferenceDataResponse` provides `MessageTypeId`, `ThreadTypeId`, and
`ReactionTypes` (`Id`, `Name`, `Emoji`). Use that thread type for direct or group
creation; do not infer direct/group from type or member count. Thread detail/list DTOs
expose `IsDirect` from the direct-thread index.

`ThreadMessageItemResponse.Reactions` contains grouped badges with `TypeId`, `Name`,
`Emoji`, `Count`, and nullable `MyReactionId`. Counts and caller reaction IDs are
loaded in two batched database queries for the already-authorized message page.
Use the existing ReactAsync/DeleteReactionAsync for changes and refresh on reaction
events. The detailed read adds reaction `Id`, `MessageId`, `MemberId`,
`CredentialId`, and `CreatedAt`.

Replies use the normal history pagination and message visibility rules. The parent
must itself be visible. A null ParentMessageId preserves the existing full timeline.

# Dedicated Yap development service

The normal develop deployment provisions a unique Yap client credential and generation
in the protected environment file, then registers `XFramework.Yap` in IdentityServer.
It does not reuse Portal credentials. The protected server-side handoff is
`/opt/xframework/client-config/yap.service-identity.json` (owner-only mode 0600).
Import it into the separately hosted Yap server's protected configuration; never commit,
log, or send its contents to a browser.

Allowed audiences: XFramework.Bolt.Hub, XFramework.IdentityServer,
XFramework.Communications, XFramework.Storage.
Allowed scopes: bolt.service, communications.chat, identity.session.validate,
datacontext.query, storage.read, storage.write. No administration, generic mutation,
cross-tenant query, or tenant-targeting scope is granted.

Deployment remains PR → CI → develop → the existing all-services deployment workflow.
The provisioning script is invoked by that workflow, not a manual alternative deployment.
