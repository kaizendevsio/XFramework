---
title: "Communications and Community Services Production Readiness"
date: 2026-04-01
category: workflow-issues
module: XFramework.Communications
problem_type: workflow_issue
component: service_object
severity: high
applies_when:
  - "Hardening Communications and Community service endpoints, validators, DI registration, contracts, migrations, and OpenAPI exposure for production readiness"
tags: [communications, community, production-readiness, endpoints, validation]
---

# Communications & Community Services — Production Readiness (Groups A & B)

**Date:** 2026-04-01
**Status:** Approved
**Scope:** Quick fixes + new endpoints for both Communications and Community services. Excludes real-time/push notifications (in progress separately).

---

## Group A — Quick Fixes

### A1. Community DI Registration

**Problem:** `IConnectionService`, `IFeedService`, `INotificationService` are implemented but never registered in DI. Any endpoint using them will throw `InvalidOperationException` at runtime.

**Fix:**
- Add all three to `Community.Api/Installers/ServicesInstaller.cs`
- Remove duplicate `ICommunityService`/`IContentService` registrations from `Program.cs` (keep only in `ServicesInstaller.cs`)

### A2. `RecievedAt` Typo

**Problem:** `MessageDirect.RecievedAt` is misspelled. Affects entity, EF config, service code, and request contracts.

**Fix:**
- Rename property to `ReceivedAt` across: `MessageDirect` entity, `MessageDirectConfiguration`, `CommunicationsService`, `UpdateMessageDirectRequest`, and any response contracts referencing it.
- EF migration required for column rename.

### A3. Pagination Bounds Validation

**Problem:** Communications list validators don't consistently enforce PageSize/PageIndex bounds. Community already does.

**Fix:** Add to all Communications paginated request validators:
- `PageSize`: `InclusiveBetween(1, 100)`
- `PageIndex`: `GreaterThanOrEqualTo(0)`

Affected validators: `GetThreadListValidator`, `GetThreadMessagesValidator`, `GetMessageFilesValidator`.

### A4. Hard-coded GUIDs to Constants

**Problem:** `CommunityService.CreateCommunityIdentityAsync` has hard-coded GUIDs for ProfilePhoto type, CoverPhoto type, and PNG storage type.

**Fix:**
- Create `Community.Domain.Shared/Constants.cs` following the Communications `Constants.cs` pattern
- Define `CommunityIdentityFileTypes.ProfilePhoto`, `CommunityIdentityFileTypes.CoverPhoto`, `StorageFileTypes.Png`
- Replace hard-coded GUIDs in `CommunityService`

---

## Group B — New Endpoints

### B5. Communications: Update Thread

**Endpoint:** `PATCH /api/threads/{id}` + `[BoltHandler]`
**Location:** `Communications.Api/Features/Threads/Update/`

**Request — `UpdateThreadRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `ThreadId` | Guid | Required |
| `RequesterCredentialId` | Guid | Required |
| `Name` | string? | MaxLength(200) |
| `Description` | string? | MaxLength(1000) |

**Service method:** `IThreadService.UpdateThreadAsync`

**Logic:**
1. Validate thread exists
2. Validate requester is a thread member
3. Apply non-null fields (partial update pattern)
4. Set `ModifiedAt = DateTime.UtcNow`
5. Return `CmdResponse` (200)

### B6. Communications: Read Receipts

#### B6a. Mark Messages Read

**Endpoint:** `POST /api/threads/{threadId}/messages/read` + `[BoltHandler]`
**Location:** `Communications.Api/Features/Messages/MarkRead/`

**Request — `MarkMessagesReadRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `ThreadId` | Guid | Required |
| `RequesterCredentialId` | Guid | Required |
| `MessageIds` | List\<Guid\> | NotEmpty, each NotEmpty |

**Service method:** `IThreadService.MarkMessagesReadAsync`

**Logic:**
1. Validate requester is thread member
2. Fetch existing `MessageDelivery` records for this member + these messages
3. For records with type "Delivered" → update to "Read" type
4. For messages with no delivery record → create with "Read" type
5. Skip messages already marked "Read"
6. Return `CmdResponse` with count

#### B6b. Auto-create Delivery Records on Fetch

**Modify:** `ThreadService.GetThreadMessagesAsync`

After fetching paginated messages, for each message that lacks a `MessageDelivery` record for the requesting member:
- Create a `MessageDelivery` with type "Delivered"
- Batch insert for efficiency

#### B6c. Constants

Add to `Communications.Domain.Shared/Constants.cs`:
```
MessageDeliveryTypes.Delivered = <guid>
MessageDeliveryTypes.Read = <guid>
```

These must match seed data in the database.

### B7. Communications: Fix Reactions (Schema Change)

**Schema change:** Add `MessageThreadMemberId` (Guid, FK) to `MessageReaction` entity.

**Entity update:** `MessageReaction.cs`
- Add `MessageThreadMemberId` property
- Add `MessageThreadMember` navigation property

**Configuration update:** `MessageReactionConfiguration.cs`
- Add FK relationship to `MessageThreadMember`

**Service updates in `ThreadService`:**
- `CreateMessageReactionAsync`: Store `MessageThreadMemberId`. Change duplicate check to `MessageId + TypeId + MessageThreadMemberId`.
- `DeleteMessageReactionAsync`: Validate `reaction.MessageThreadMemberId == requester's member ID`.

**Migration required** for new column + FK.

### B8. Community: Edit Content

**Endpoint:** `PATCH /api/community/content/{id}` + `[BoltHandler]`
**Location:** `Community.Api/Features/Content/Edit/`

**Request — `EditContentRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `ContentId` | Guid | Required |
| `RequestingIdentityId` | Guid | Required |
| `Text` | string? | MaxLength(5000) |
| `Title` | string? | MaxLength(200) |

**Contracts:** `EditContentRequest` in `Community.Domain.Shared/Contracts/Requests/`, implements `ICommand<CmdResponse>` + `IBoltRequest<EditContentRequest, CmdResponse>`

**Service method:** `IContentService.EditContentAsync`

**Logic:**
1. Validate content exists and is not deleted
2. Validate `SocialMediaIdentityId == RequestingIdentityId` (owner only)
3. Apply non-null fields
4. Set `ModifiedAt = DateTime.UtcNow`
5. Return `CmdResponse` (200)

### B9. Community: Content File Endpoints

**Location:** `Community.Api/Features/Content/Files/`

#### B9a. Attach File

**Endpoint:** `POST /api/community/content/{contentId}/files` + `[BoltHandler]`

**Request — `CreateContentFileRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `ContentId` | Guid | Required |
| `StorageFileId` | Guid | Required |
| `RequestingIdentityId` | Guid | Required |

**Logic:**
1. Validate content exists
2. Validate requester is content author
3. Create `CommunityContentFile` with `ContentId` + `StorageId`
4. Return `CmdResponse` (201)

#### B9b. List Files

**Endpoint:** `GET /api/community/content/{contentId}/files` + `[BoltHandler]`

**Request — `GetContentFilesRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `ContentId` | Guid | Required |

**Response — `ContentFileResponse`:**
| Field | Type |
|-------|------|
| `Id` | Guid |
| `ContentId` | Guid |
| `StorageFileId` | Guid |
| `CreatedAt` | DateTime |

**Logic:**
1. Validate content exists
2. Return list of `CommunityContentFile` records for that content

#### B9c. Remove File

**Endpoint:** `DELETE /api/community/content/{contentId}/files/{fileId}` + `[BoltHandler]`

**Request — `DeleteContentFileRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `ContentId` | Guid | Required |
| `FileId` | Guid | Required |
| `RequestingIdentityId` | Guid | Required |

**Logic:**
1. Validate content exists
2. Validate requester is content author
3. Soft-delete the `CommunityContentFile`
4. Return `CmdResponse` (200)

### B10. Community: Identity File Update

**Endpoint:** `PUT /api/community/identities/{id}/files/{fileId}` + `[BoltHandler]`
**Location:** `Community.Api/Features/CommunityIdentities/Files/Update/`

**Request — `UpdateIdentityFileRequest`:**
| Field | Type | Validation |
|-------|------|------------|
| `IdentityId` | Guid | Required |
| `FileId` | Guid | Required |
| `StorageFileId` | Guid | Required |
| `RequestingIdentityId` | Guid | Required |

**Logic:**
1. Validate `RequestingIdentityId == IdentityId` (owner only)
2. Find `CommunityIdentityFile` by `FileId`
3. Validate file belongs to this identity
4. Update `StorageId` to new `StorageFileId`
5. Set `ModifiedAt`
6. Return `CmdResponse` (200)

### B11. Community: Full Block Enforcement

**Block connection type:** Add `CommunityConnectionTypes.Block` GUID constant to `Community.Domain.Shared/Constants.cs`.

**Helper method on `IConnectionService`:**
```csharp
Task<bool> IsBlockedAsync(Guid identityA, Guid identityB, CancellationToken ct);
```
Checks for any active (non-deleted, enabled) `CommunityConnection` where type is "Block" in either direction between the two identities.

**Enforcement points:**

| Service Method | Block Behavior |
|----------------|----------------|
| `FeedService.GetFeedAsync` | Exclude content authored by identities that blocked the viewer AND by identities the viewer has blocked |
| `ContentService.SearchIdentitiesAsync` | Exclude identities blocked in either direction |
| `ContentService.GetCommunityIdentityAsync` | Return 404 if either party has blocked the other |
| `ConnectionService.CreateConnectionAsync` | Return 403 if block exists in either direction |
| `ContentService.CreateContentReactionAsync` | Return 403 if content author and reactor are blocked in either direction |
| `ContentService.GetContentAsync` | Return 404 if content author and requester are blocked in either direction |
| `ContentService.DeleteContentAsync` | No block check needed (owner-only operation) |

**Implementation approach:** Inject `IConnectionService` into `ContentService` and `FeedService`. For feed queries, pre-fetch the blocked identity set for the requesting user and filter with a `WHERE NOT IN` clause.

---

## Out of Scope

- Real-time/push notifications (in progress separately)
- Typing indicators
- Group C features (search, moderation, groups, privacy, @mentions, scheduled messages, admin CRUD, role enforcement)
- Group D (tests) — will be designed separately after A & B are implemented
