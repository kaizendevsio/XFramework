---
title: "Actor-owned chat attachment uploads"
date: 2026-09-11
category: architecture-patterns
module: Communications, Storage
problem_type: authorization
component: attachment-upload
severity: high
applies_when:
  - "Uploading or downloading chat attachments through the Communications SDK"
tags: [communications, storage, bolt, authorization, uploads]
status: current
---

# Actor-owned chat attachment uploads

Ordinary members must not provision arbitrary Storage reference data or gain
`storage:manage` to attach a file. Storage owns upload persistence and object-provider
operations; Communications owns membership, message visibility, and attachment links.

## SDK workflow

1. Bind `ICommunicationsChatClient.ForCurrentActorAsync` to the signed-in actor.
2. Call the session's `CreateAttachmentUploadAsync(CreateChatAttachmentUploadRequest)`
   with the thread ID, original filename, MIME type, total byte length, and optional
   chunk size/SHA-256. Communications checks active membership and attachment policy.
3. Send negotiated chunks through `IStorageServiceWrapper.UploadChatStorageFilePart`
   using `UploadChatStorageFilePartRequest`, then complete with
   `CompleteChatStorageUploadSessionRequest`. Forward the same actor token for each
   call. Use `AbortChatStorageUploadSessionRequest` on cancellation.
4. Multipart completion can return `Verifying`; wait for Storage status `Available`
   before linking via the existing `CreateMessageFile` operation. Storage verifies
   the uploader, tenant, thread, purpose, and availability before Communications links.
5. Download with the session's `GetAttachmentDownloadUrlAsync(threadId, messageId,
   fileId)`. `fileId` is the **MessageFile link ID**, not the StorageFile ID.
   Communications checks membership, message visibility, and the active link before
   Storage issues a private signed URL. Another authorized member can download;
   another user cannot finish or abort the uploader's session.

## Boundaries

- Ordinary actors need Storage View/Create and the relevant Communications capability.
  The calling app needs `storage.read`, `storage.write`, and `communications.chat`,
  not admin or tenant-target scopes.
- Storage's chat-session creation, reference validation, and signed-URL handlers accept
  only the trusted Communications service caller. Part/complete/abort accept the
  owning actor through the app's regular Storage wrapper.
- Metadata is fixed internally: `application/octet-stream` type, `Communications`
  identifier group, `Chat attachments` identifier. The file itself retains its actual
  filename and MIME type. No client-selected owner or purpose is accepted.
- `StorageFile.UploadedByCredentialId` and `UploadPurpose` are nullable additive
  columns, introduced by `ChatUploadOwnership` through the normal migration runner.
  Chat files use purpose `communications.chat`, private visibility, and the existing
  `Identifier` field for their thread binding. Legacy files remain purpose-null;
  they cannot be newly attached through the bounded chat path.
- Generic download URLs cannot bypass Communications visibility checks for chat files.
  Generic upload mutations also enforce ownership on chat-purpose files. Existing
  generic metadata-management authorization is unchanged.
- Existing legacy attachment links need an explicit ownership/provenance migration
  before using this new download path; do not infer ownership or grant admin access.

## Verification

`ChatStorageUploadTests` runs against PostgreSQL and real generated Bolt handlers for
single/multipart completion, wrong actor/tenant, missing capability, disabled feature,
and unchanged admin metadata boundaries. `ThreadServiceSecurityTests` covers active
membership, visible messages, linked files, and rejected ownership validation.
`StorageBoltScopeContractTests` locks down service scopes and internal caller policy;
`CommunicationsChatClientTests` covers actor/tenant/cancellation forwarding.
