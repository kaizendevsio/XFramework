# Communications & Community Production Readiness Implementation Plan

> Historical plan migrated from `docs/superpowers/`. For new implementation planning, use `/ce-plan`; this checklist is retained as context.

**Goal:** Fix critical bugs and add missing endpoints across Communications and Community services to reach production readiness (Groups A & B from the spec).

**Architecture:** Both services follow Vertical Slice Architecture with static endpoint classes, FluentValidation, service layer returning `Result<T>`, and `[BoltHandler]` + `[MapX]` attributes for dual HTTP/Bolt transport. All request contracts use `[MemoryPackable] partial record` implementing `IBoltRequest<TRequest, TResponse>`.

**Tech Stack:** .NET 10, C# 14, EF Core (PostgreSQL), FluentValidation, MemoryPack, Bolt (SignalR RPC)

---

## File Map

### Group A (Quick Fixes)
| Action | File |
|--------|------|
| Modify | `src/Modules/XFramework.Community/Community.Api/Installers/ServicesInstaller.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Program.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/MessageDirect.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/Requests/Update/UpdateMessageDirectRequest.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Api/Services/CommunicationsService.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/UpdateDirect/UpdateDirectMessageValidator.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/GetMessages/GetThreadMessagesValidator.cs` |
| Create | `src/Modules/XFramework.Communications/Communications.Api/Features/Threads/GetList/GetThreadListValidator.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Constants.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Services/CommunityService.cs` |

### Group B (New Endpoints)
| Action | File |
|--------|------|
| Create | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/Requests/Threads/UpdateThreadRequest.cs` |
| Create | `src/Modules/XFramework.Communications/Communications.Api/Features/Threads/Update/Endpoint.cs` |
| Create | `src/Modules/XFramework.Communications/Communications.Api/Features/Threads/Update/UpdateThreadValidator.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Api/Services/IThreadService.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Api/Services/ThreadService.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Constants.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/MessageReaction.cs` |
| Modify | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Configurations/MessageReactionConfiguration.cs` |
| Create | `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/Requests/Threads/MarkMessagesReadRequest.cs` |
| Create | `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/MarkRead/Endpoint.cs` |
| Create | `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/MarkRead/MarkMessagesReadValidator.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/EditContentRequest.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Edit/Endpoint.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Edit/EditContentValidator.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Services/IContentService.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Services/ContentService.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/CreateContentFileRequest.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/GetContentFilesRequest.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/DeleteContentFileRequest.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Responses/ContentFileResponse.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Create/Endpoint.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Create/CreateContentFileValidator.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/GetList/Endpoint.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/GetList/GetContentFilesValidator.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Delete/Endpoint.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Delete/DeleteContentFileValidator.cs` |
| Create | `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/UpdateIdentityFileRequest.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/CommunityIdentities/Files/Update/Endpoint.cs` |
| Create | `src/Modules/XFramework.Community/Community.Api/Features/CommunityIdentities/Files/Update/UpdateIdentityFileValidator.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Services/IConnectionService.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Services/ConnectionService.cs` |
| Modify | `src/Modules/XFramework.Community/Community.Api/Services/FeedService.cs` |

---

### Task 1: Community DI Registration Fix (A1)

**Files:**
- Modify: `src/Modules/XFramework.Community/Community.Api/Installers/ServicesInstaller.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Program.cs`

- [ ] **Step 1: Add missing service registrations to ServicesInstaller.cs**

In `Community.Api/Installers/ServicesInstaller.cs`, add the three missing services after line 17:

```csharp
services.AddScoped<IConnectionService, ConnectionService>();
services.AddScoped<IFeedService, FeedService>();
services.AddScoped<INotificationService, NotificationService>();
```

The full `InstallServices` method should be:

```csharp
public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
{
    /*services.AddSingleton<ICachingService, CachingService>();*/
    services.AddTenantResolver();

    // Register Community Services (VSA Architecture)
    services.AddScoped<ICommunityService, CommunityService>();
    services.AddScoped<IContentService, ContentService>();
    services.AddScoped<IConnectionService, ConnectionService>();
    services.AddScoped<IFeedService, FeedService>();
    services.AddScoped<INotificationService, NotificationService>();
}
```

- [ ] **Step 2: Remove duplicate registrations from Program.cs**

In `Community.Api/Program.cs`, remove lines 17-19 (the duplicate service registrations):

```csharp
// Register Community services
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<IContentService, ContentService>();
```

These are already in `ServicesInstaller.cs`. The file should go directly from the health checks block to the validators block.

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/Modules/XFramework.Community/Community.Api/Installers/ServicesInstaller.cs src/Modules/XFramework.Community/Community.Api/Program.cs
git commit -m "fix: Register missing Community DI services (Connection, Feed, Notification)"
```

---

### Task 2: RecievedAt Typo Fix (A2)

**Files:**
- Modify: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/MessageDirect.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/Requests/Update/UpdateMessageDirectRequest.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Services/CommunicationsService.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/UpdateDirect/UpdateDirectMessageValidator.cs`

- [ ] **Step 1: Fix MessageDirect entity**

In `Communications.Domain.Shared/Contracts/MessageDirect.cs`, rename property at line 52:

Old: `public DateTime? RecievedAt { get; set; }`
New: `public DateTime? ReceivedAt { get; set; }`

- [ ] **Step 2: Fix UpdateMessageDirectRequest**

In `Communications.Domain.Shared/Contracts/Requests/Update/UpdateMessageDirectRequest.cs`, rename property at line 13:

Old: `public DateTime? RecievedAt { get; set; }`
New: `public DateTime? ReceivedAt { get; set; }`

- [ ] **Step 3: Fix CommunicationsService**

In `Communications.Api/Services/CommunicationsService.cs`, at line 125:

Old: `record.RecievedAt = request.RecievedAt;`
New: `record.ReceivedAt = request.ReceivedAt;`

- [ ] **Step 4: Fix UpdateDirectMessageValidator**

In `Communications.Api/Features/Messages/UpdateDirect/UpdateDirectMessageValidator.cs`, replace all `RecievedAt` references:

```csharp
RuleFor(x => x.ReceivedAt)
    .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("ReceivedAt cannot be in the future")
    .When(x => x.ReceivedAt.HasValue);

RuleFor(x => x.ReceivedAt)
    .GreaterThanOrEqualTo(x => x.SentAt).WithMessage("ReceivedAt must be after SentAt")
    .When(x => x.SentAt.HasValue && x.ReceivedAt.HasValue);
```

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/Modules/XFramework.Communications/
git commit -m "fix: Rename RecievedAt to ReceivedAt across MessageDirect"
```

Note: EF migration for column rename will be handled after all schema changes are complete (Task 7 also has schema changes).

---

### Task 3: Pagination Bounds Validation (A3)

**Files:**
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/GetMessages/GetThreadMessagesValidator.cs`
- Create: `src/Modules/XFramework.Communications/Communications.Api/Features/Threads/GetList/GetThreadListValidator.cs`

- [ ] **Step 1: Fix GetThreadMessagesValidator**

In `Communications.Api/Features/Messages/GetMessages/GetThreadMessagesValidator.cs`, replace the existing PageSize rule (lines 16-18) and add PageIndex:

```csharp
RuleFor(x => x.PageSize)
    .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

RuleFor(x => x.PageIndex)
    .GreaterThanOrEqualTo(0).WithMessage("Page index must be 0 or greater");
```

- [ ] **Step 2: Create GetThreadListValidator**

Create file `Communications.Api/Features/Threads/GetList/GetThreadListValidator.cs`:

```csharp
using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Threads.GetList;

public sealed class GetThreadListValidator : AbstractValidator<GetThreadListRequest>
{
    public GetThreadListValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Page index must be 0 or greater");
    }
}
```

Note: `GetMessageFilesRequest` doesn't have pagination fields, so no validator change needed there.

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/Modules/XFramework.Communications/Communications.Api/Features/
git commit -m "fix: Add pagination bounds validation to Communications list endpoints"
```

---

### Task 4: Hard-coded GUIDs to Constants (A4)

**Files:**
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Constants.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/CommunityService.cs`

- [ ] **Step 1: Create Community Constants.cs**

Create file `Community.Domain.Shared/Constants.cs`:

```csharp
namespace Community.Domain.Shared;

public static class CommunityIdentityFileTypes
{
    public static readonly Guid ProfilePhoto = new("996dd417-170c-4ac9-b565-62caf4ab5ccf");
    public static readonly Guid CoverPhoto = new("8716ec30-b061-45cc-ad5b-77bda960d90e");
}

public static class CommunityStorageFileTypes
{
    public static readonly Guid Png = new("af6b9396-ba01-4f88-a5d0-e0cfbc038146");
}

public static class CommunityConnectionTypes
{
    public static readonly Guid Follow = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid Block = new("a0000000-0000-0000-0000-000000000002");
}
```

Note: `CommunityConnectionTypes` GUIDs are placeholders — they must match the actual seed data in your database. Verify and replace with the real GUIDs from your DB seed before deploying.

- [ ] **Step 2: Replace hard-coded GUIDs in CommunityService**

In `Community.Api/Services/CommunityService.cs`, add using at top:

```csharp
using Community.Domain.Shared;
```

Then in `CreateCommunityIdentityAsync`, replace line 72:

Old: `var pngType = storageFileTypes.FirstOrDefault(i => i.Id == new Guid("af6b9396-ba01-4f88-a5d0-e0cfbc038146"));`
New: `var pngType = storageFileTypes.FirstOrDefault(i => i.Id == CommunityStorageFileTypes.Png);`

Replace line 91:

Old: `Type = identityFileTypes.FirstOrDefault(i => i.Id == new Guid("996dd417-170c-4ac9-b565-62caf4ab5ccf")),`
New: `Type = identityFileTypes.FirstOrDefault(i => i.Id == CommunityIdentityFileTypes.ProfilePhoto),`

Replace line 101:

Old: `Type = identityFileTypes.FirstOrDefault(i => i.Id == new Guid("8716ec30-b061-45cc-ad5b-77bda960d90e")),`
New: `Type = identityFileTypes.FirstOrDefault(i => i.Id == CommunityIdentityFileTypes.CoverPhoto),`

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/Modules/XFramework.Community/Community.Domain.Shared/Constants.cs src/Modules/XFramework.Community/Community.Api/Services/CommunityService.cs
git commit -m "refactor: Extract hard-coded GUIDs to Community Constants.cs"
```

---

### Task 5: Communications — Update Thread Endpoint (B5)

**Files:**
- Create: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/Requests/Threads/UpdateThreadRequest.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Services/IThreadService.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Services/ThreadService.cs`
- Create: `src/Modules/XFramework.Communications/Communications.Api/Features/Threads/Update/Endpoint.cs`
- Create: `src/Modules/XFramework.Communications/Communications.Api/Features/Threads/Update/UpdateThreadValidator.cs`

- [ ] **Step 1: Create UpdateThreadRequest**

Create file `Communications.Domain.Shared/Contracts/Requests/Threads/UpdateThreadRequest.cs`:

```csharp
namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = UpdateThreadRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateThreadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
```

- [ ] **Step 2: Add method to IThreadService**

In `Communications.Api/Services/IThreadService.cs`, add after the `GetThreadAsync` method (after line 17):

```csharp
Task<Result<CmdResponse>> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default);
```

- [ ] **Step 3: Implement UpdateThreadAsync in ThreadService**

In `Communications.Api/Services/ThreadService.cs`, add the method (before the `AddThreadMemberAsync` method):

```csharp
public async Task<Result<CmdResponse>> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default)
{
    try
    {
        var thread = await dataContext.Query<MessageThread>()
            .Where(t => t.Id == request.ThreadId)
            .Where(t => !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (thread is null)
            return Result<CmdResponse>.NotFound("Thread not found");

        // Validate requester is a member
        var member = await dataContext.Query<MessageThreadMember>()
            .Where(m => m.MessageThreadId == request.ThreadId)
            .Where(m => m.CredentialId == request.RequesterCredentialId)
            .Where(m => !m.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (member is null)
            return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

        if (request.Name is not null)
            thread.Name = request.Name;

        if (request.Description is not null)
            thread.Description = request.Description;

        thread.ModifiedAt = DateTime.UtcNow;

        dataContext.Update(thread);
        await dataContext.SaveChangesAsync(ct);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Thread updated successfully"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating thread {ThreadId}", request.ThreadId);
        return Result<CmdResponse>.Failure($"Error updating thread: {ex.Message}");
    }
}
```

- [ ] **Step 4: Create Endpoint**

Create file `Communications.Api/Features/Threads/Update/Endpoint.cs`:

```csharp
using Communications.Api.Services;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.Update;

public static class UpdateThreadEndpoint
{
    [BoltHandler]
    [MapPatch("/api/threads/{threadId:guid}", Tags = ["Threads"],
        Summary = "Update a thread",
        Description = "Updates thread name and/or description. Validates the requester is a member.")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateThreadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.UpdateThreadAsync(request, ct);
    }
}
```

- [ ] **Step 5: Create Validator**

Create file `Communications.Api/Features/Threads/Update/UpdateThreadValidator.cs`:

```csharp
using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Threads.Update;

public sealed class UpdateThreadValidator : AbstractValidator<UpdateThreadRequest>
{
    public UpdateThreadValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester Credential ID is required");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => x.Description is not null);
    }
}
```

- [ ] **Step 6: Verify build**

Run: `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/Modules/XFramework.Communications/
git commit -m "feat: Add Update Thread endpoint (PATCH /api/threads/{id})"
```

---

### Task 6: Communications — Read Receipts (B6)

**Files:**
- Modify: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Constants.cs`
- Create: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/Requests/Threads/MarkMessagesReadRequest.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Services/IThreadService.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Services/ThreadService.cs`
- Create: `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/MarkRead/Endpoint.cs`
- Create: `src/Modules/XFramework.Communications/Communications.Api/Features/Messages/MarkRead/MarkMessagesReadValidator.cs`

- [ ] **Step 1: Add delivery type constants**

In `Communications.Domain.Shared/Constants.cs`, add after the `GenericSender` class:

```csharp
public static class MessageDeliveryTypes
{
    public static readonly Guid Delivered = new("b1000000-0000-0000-0000-000000000001");
    public static readonly Guid Read = new("b1000000-0000-0000-0000-000000000002");
}
```

Note: These GUIDs must match seed data in your `MessageDeliveryType` table. Verify and replace before deploying.

- [ ] **Step 2: Create MarkMessagesReadRequest**

Create file `Communications.Domain.Shared/Contracts/Requests/Threads/MarkMessagesReadRequest.cs`:

```csharp
namespace Communications.Domain.Shared.Contracts.Requests.Threads;

using TRequest = MarkMessagesReadRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record MarkMessagesReadRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ThreadId { get; set; }
    public Guid RequesterCredentialId { get; set; }
    public List<Guid> MessageIds { get; set; } = [];
}
```

- [ ] **Step 3: Add method to IThreadService**

In `Communications.Api/Services/IThreadService.cs`, add after the `DeleteMessageReactionAsync` method:

```csharp
// Round 3: Read Receipts
Task<Result<CmdResponse>> MarkMessagesReadAsync(MarkMessagesReadRequest request, CancellationToken ct = default);
```

- [ ] **Step 4: Implement MarkMessagesReadAsync in ThreadService**

Add this method to `ThreadService.cs` (before the closing brace of the class):

```csharp
public async Task<Result<CmdResponse>> MarkMessagesReadAsync(MarkMessagesReadRequest request, CancellationToken ct = default)
{
    try
    {
        var member = await dataContext.Query<MessageThreadMember>()
            .Where(m => m.MessageThreadId == request.ThreadId)
            .Where(m => m.CredentialId == request.RequesterCredentialId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (member is null)
            return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

        // Get existing deliveries for this member on these messages
        var existingDeliveries = await dataContext.Query<MessageDelivery>()
            .Where(d => d.MessageThreadMemberId == member.Id)
            .Where(d => request.MessageIds.Contains(d.MessageId))
            .Where(d => !d.IsDeleted)
            .ToListAsync(ct);

        var existingByMessage = existingDeliveries.ToDictionary(d => d.MessageId);
        var markedCount = 0;

        foreach (var messageId in request.MessageIds)
        {
            if (existingByMessage.TryGetValue(messageId, out var delivery))
            {
                // Already "Read" — skip
                if (delivery.TypeId == MessageDeliveryTypes.Read)
                    continue;

                // Upgrade "Delivered" to "Read"
                delivery.TypeId = MessageDeliveryTypes.Read;
                delivery.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(delivery);
                markedCount++;
            }
            else
            {
                // No delivery record — create as "Read"
                dataContext.Add(new MessageDelivery
                {
                    MessageThreadMemberId = member.Id,
                    MessageId = messageId,
                    TypeId = MessageDeliveryTypes.Read,
                    IsEnabled = true
                });
                markedCount++;
            }
        }

        await dataContext.SaveChangesAsync(ct);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = $"{markedCount} message(s) marked as read"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error marking messages as read in thread {ThreadId}", request.ThreadId);
        return Result<CmdResponse>.Failure($"Error marking messages as read: {ex.Message}");
    }
}
```

- [ ] **Step 5: Add auto-delivery to GetThreadMessagesAsync**

In `ThreadService.cs`, in the `GetThreadMessagesAsync` method, add the following block after the messages are fetched and before the member info lookup (after line 405, before line 407):

```csharp
// Auto-create "Delivered" records for messages this member hasn't seen
var messageIds = messages.Select(m => m.Id).ToList();
var existingDeliveries = await dataContext.Query<MessageDelivery>()
    .Where(d => d.MessageThreadMemberId == requesterMember.Id)
    .Where(d => messageIds.Contains(d.MessageId))
    .Where(d => !d.IsDeleted)
    .Select(d => d.MessageId)
    .ToListAsync(ct);

var undeliveredIds = messageIds.Except(existingDeliveries).ToList();
if (undeliveredIds.Count > 0)
{
    foreach (var msgId in undeliveredIds)
    {
        dataContext.Add(new MessageDelivery
        {
            MessageThreadMemberId = requesterMember.Id,
            MessageId = msgId,
            TypeId = MessageDeliveryTypes.Delivered,
            IsEnabled = true
        });
    }
    await dataContext.SaveChangesAsync(ct);
}
```

Add the using at top of `ThreadService.cs` if not already present:

```csharp
using Communications.Domain.Shared;
```

- [ ] **Step 6: Create Endpoint**

Create file `Communications.Api/Features/Messages/MarkRead/Endpoint.cs`:

```csharp
using Communications.Api.Services;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.MarkRead;

public static class MarkMessagesReadEndpoint
{
    [BoltHandler]
    [MapPost("/api/threads/{threadId:guid}/messages/read", Tags = ["Messages"],
        Summary = "Mark messages as read",
        Description = "Marks the specified messages as read for the requesting member. Creates delivery records if they don't exist.")]
    public static async Task<Result<CmdResponse>> Handle(
        MarkMessagesReadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.MarkMessagesReadAsync(request, ct);
    }
}
```

- [ ] **Step 7: Create Validator**

Create file `Communications.Api/Features/Messages/MarkRead/MarkMessagesReadValidator.cs`:

```csharp
using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Messages.MarkRead;

public sealed class MarkMessagesReadValidator : AbstractValidator<MarkMessagesReadRequest>
{
    public MarkMessagesReadValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester Credential ID is required");

        RuleFor(x => x.MessageIds)
            .NotEmpty().WithMessage("At least one message ID is required");

        RuleForEach(x => x.MessageIds)
            .NotEmpty().WithMessage("Message ID cannot be empty");
    }
}
```

- [ ] **Step 8: Verify build**

Run: `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 9: Commit**

```bash
git add src/Modules/XFramework.Communications/
git commit -m "feat: Add read receipts — MarkMessagesRead endpoint + auto-delivery on fetch"
```

---

### Task 7: Communications — Fix Reactions Schema (B7)

**Files:**
- Modify: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Contracts/MessageReaction.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Domain.Shared/Configurations/MessageReactionConfiguration.cs`
- Modify: `src/Modules/XFramework.Communications/Communications.Api/Services/ThreadService.cs`

- [ ] **Step 1: Add MessageThreadMemberId to MessageReaction entity**

In `Communications.Domain.Shared/Contracts/MessageReaction.cs`, add after line 12 (after `TypeId`):

```csharp
[MemoryPackOrder(4)]
public Guid MessageThreadMemberId { get; set; }

[MemoryPackOrder(5)]
public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
```

Full file should be:

```csharp
namespace Communications.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageReaction : BaseModel
{

    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(2)]
    public virtual MessageReactionType Type { get; set; } = null!;

    [MemoryPackOrder(3)]
    public virtual Message Message { get; set; } = null!;

    [MemoryPackOrder(4)]
    public Guid MessageThreadMemberId { get; set; }

    [MemoryPackOrder(5)]
    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}
```

- [ ] **Step 2: Add FK to MessageReactionConfiguration**

In `Communications.Domain.Shared/Configurations/MessageReactionConfiguration.cs`, add after line 32 (after the message FK):

```csharp
entity.HasOne(d => d.MessageThreadMember).WithMany()
    .HasForeignKey(d => d.MessageThreadMemberId)
    .OnDelete(DeleteBehavior.ClientSetNull)
    .HasConstraintName("messagereaction_messagethreadmember_id_fk");
```

- [ ] **Step 3: Fix CreateMessageReactionAsync**

In `ThreadService.cs`, in `CreateMessageReactionAsync`, replace the duplicate check (lines 646-649) with:

```csharp
var duplicateExists = await dataContext.Query<MessageReaction>()
    .Where(r => r.MessageId == request.MessageId)
    .Where(r => r.TypeId == request.TypeId)
    .Where(r => r.MessageThreadMemberId == member.Id)
    .Where(r => !r.IsDeleted && r.IsEnabled)
    .AnyAsync(ct);
```

Replace the reaction creation (lines 654-659) with:

```csharp
var reaction = new MessageReaction
{
    MessageId = request.MessageId,
    TypeId = request.TypeId,
    MessageThreadMemberId = member.Id,
    IsEnabled = true
};
```

- [ ] **Step 4: Fix DeleteMessageReactionAsync**

In `ThreadService.cs`, in `DeleteMessageReactionAsync`, after finding the reaction and verifying the message exists, replace the member validation (lines 696-702) with ownership check:

```csharp
var member = await dataContext.Query<MessageThreadMember>()
    .Where(m => m.MessageThreadId == message.MessageThreadId)
    .Where(m => m.CredentialId == request.RequesterCredentialId)
    .Where(m => !m.IsDeleted && m.IsEnabled)
    .FirstOrDefaultAsync(ct);

if (member is null)
    return Result<CmdResponse>.Failure("Requester is not a member of this thread", 403);

if (reaction.MessageThreadMemberId != member.Id)
    return Result<CmdResponse>.Forbidden("You can only delete your own reactions");
```

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/Modules/XFramework.Communications/
git commit -m "feat: Track who reacted — add MessageThreadMemberId to MessageReaction"
```

---

### Task 8: Community — Edit Content Endpoint (B8)

**Files:**
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/EditContentRequest.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/IContentService.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/ContentService.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Edit/Endpoint.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Edit/EditContentValidator.cs`

- [ ] **Step 1: Create EditContentRequest**

Create file `Community.Domain.Shared/Contracts/Requests/EditContentRequest.cs`:

```csharp
namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record EditContentRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<EditContentRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid RequestingIdentityId { get; set; }
    public string? Text { get; set; }
    public string? Title { get; set; }
}
```

- [ ] **Step 2: Add method to IContentService**

In `Community.Api/Services/IContentService.cs`, add after `DeleteContentAsync`:

```csharp
/// <summary>
/// Edits content owned by the requester (partial update)
/// </summary>
Task<Result<CmdResponse>> EditContentAsync(
    EditContentRequest request,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Implement EditContentAsync in ContentService**

In `Community.Api/Services/ContentService.cs`, add after `DeleteContentAsync` (after line 214):

```csharp
/// <inheritdoc />
public async Task<Result<CmdResponse>> EditContentAsync(
    EditContentRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        var content = await _dataContext.Query<CommunityContent>()
            .Where(c => c.Id == request.ContentId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (content == null)
        {
            _logger.EntityNotFound("CommunityContent", request.ContentId);
            return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");
        }

        if (content.SocialMediaIdentityId != request.RequestingIdentityId)
        {
            _logger.BusinessRuleViolation("EditContent", $"Identity {request.RequestingIdentityId} does not own content {request.ContentId}");
            return Result<CmdResponse>.Forbidden("You do not have permission to edit this content");
        }

        if (request.Text is not null)
            content.Text = request.Text;

        if (request.Title is not null)
            content.Title = request.Title;

        content.ModifiedAt = DateTime.UtcNow;

        _dataContext.Update(content);
        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.EntityUpdated("CommunityContent", request.ContentId);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Content updated successfully"
        });
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("EditContent", "CommunityContent", request.ContentId, ex.Message, ex);
        return Result<CmdResponse>.Failure("An error occurred while editing content", 500);
    }
}
```

- [ ] **Step 4: Create Endpoint**

Create file `Community.Api/Features/Content/Edit/Endpoint.cs`:

```csharp
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Edit;

public static class EditContentEndpoint
{
    [BoltHandler]
    [MapPatch("/api/community/content/{id:guid}", Tags = ["Community Content"],
        Summary = "Edit content",
        Description = "Updates content text and/or title. Validates that the requester owns the content.")]
    public static async Task<Result<CmdResponse>> Handle(
        EditContentRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.EditContentAsync(request, ct);
    }
}
```

- [ ] **Step 5: Create Validator**

Create file `Community.Api/Features/Content/Edit/EditContentValidator.cs`:

```csharp
using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Edit;

public sealed class EditContentValidator : AbstractValidator<EditContentRequest>
{
    public EditContentValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");

        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");

        RuleFor(x => x.Text)
            .MaximumLength(5000).WithMessage("Text cannot exceed 5000 characters")
            .When(x => x.Text is not null);

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters")
            .When(x => x.Title is not null);
    }
}
```

- [ ] **Step 6: Verify build**

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/Modules/XFramework.Community/
git commit -m "feat: Add Edit Content endpoint (PATCH /api/community/content/{id})"
```

---

### Task 9: Community — Content File Endpoints (B9)

**Files:**
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/CreateContentFileRequest.cs` (new VSA request — NOT the legacy one in the entity file)
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/GetContentFilesRequest.cs`
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/DeleteContentFileRequest.cs`
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Responses/ContentFileResponse.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/IContentService.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/ContentService.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Create/Endpoint.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Create/CreateContentFileValidator.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/GetList/Endpoint.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/GetList/GetContentFilesValidator.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Delete/Endpoint.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/Content/Files/Delete/DeleteContentFileValidator.cs`

- [ ] **Step 1: Create request/response contracts**

Create `Community.Domain.Shared/Contracts/Requests/CreateContentFileRequest.cs`:

```csharp
namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateContentFileVsaRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateContentFileVsaRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid StorageFileId { get; set; }
    public Guid RequestingIdentityId { get; set; }
}
```

Note: Named `CreateContentFileVsaRequest` to avoid conflict with legacy `CreateCommunityContentFileRequest` in the entity file.

Create `Community.Domain.Shared/Contracts/Requests/GetContentFilesRequest.cs`:

```csharp
using Community.Domain.Shared.Contracts.Responses;

namespace Community.Domain.Shared.Contracts.Requests;

using TRequest = GetContentFilesRequest;
using TResponse = QueryResponse<List<ContentFileResponse>>;

[MemoryPackable]
public partial record GetContentFilesRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ContentId { get; set; }
}
```

Create `Community.Domain.Shared/Contracts/Requests/DeleteContentFileRequest.cs`:

```csharp
namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record DeleteContentFileRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<DeleteContentFileRequest, TResponse>
{
    public Guid ContentId { get; set; }
    public Guid FileId { get; set; }
    public Guid RequestingIdentityId { get; set; }
}
```

Create `Community.Domain.Shared/Contracts/Responses/ContentFileResponse.cs`:

```csharp
namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ContentFileResponse
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public Guid StorageFileId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 2: Add methods to IContentService**

In `Community.Api/Services/IContentService.cs`, add after `EditContentAsync`:

```csharp
/// <summary>
/// Attaches a file to content
/// </summary>
Task<Result<CmdResponse>> CreateContentFileAsync(
    CreateContentFileVsaRequest request,
    CancellationToken cancellationToken = default);

/// <summary>
/// Lists files attached to content
/// </summary>
Task<Result<List<ContentFileResponse>>> GetContentFilesAsync(
    GetContentFilesRequest request,
    CancellationToken cancellationToken = default);

/// <summary>
/// Removes a file from content
/// </summary>
Task<Result<CmdResponse>> DeleteContentFileAsync(
    DeleteContentFileRequest request,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Implement file methods in ContentService**

Add to `ContentService.cs` (before the closing brace):

```csharp
/// <inheritdoc />
public async Task<Result<CmdResponse>> CreateContentFileAsync(
    CreateContentFileVsaRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        var content = await _dataContext.Query<CommunityContent>()
            .Where(c => c.Id == request.ContentId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (content == null)
            return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");

        if (content.SocialMediaIdentityId != request.RequestingIdentityId)
            return Result<CmdResponse>.Forbidden("You do not have permission to attach files to this content");

        var entity = new CommunityContentFile
        {
            ContentId = request.ContentId,
            StorageId = request.StorageFileId,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true
        };

        _dataContext.Add(entity);
        await _dataContext.SaveChangesAsync(cancellationToken);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.Created,
            Message = "File attached successfully"
        }, 201);
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("CreateContentFile", "CommunityContentFile", Guid.Empty, ex.Message, ex);
        return Result<CmdResponse>.Failure("An error occurred while attaching file", 500);
    }
}

/// <inheritdoc />
public async Task<Result<List<ContentFileResponse>>> GetContentFilesAsync(
    GetContentFilesRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        var contentExists = await _dataContext.Query<CommunityContent>()
            .Where(c => c.Id == request.ContentId && !c.IsDeleted)
            .AnyAsync(cancellationToken);

        if (!contentExists)
            return Result<List<ContentFileResponse>>.NotFound($"Content with Id {request.ContentId} does not exist");

        var files = await _dataContext.Query<CommunityContentFile>()
            .Where(f => f.ContentId == request.ContentId && !f.IsDeleted)
            .Select(f => new ContentFileResponse
            {
                Id = f.Id,
                ContentId = f.ContentId,
                StorageFileId = f.StorageId,
                CreatedAt = f.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<ContentFileResponse>>.Success(files);
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("GetContentFiles", "CommunityContentFile", request.ContentId, ex.Message, ex);
        return Result<List<ContentFileResponse>>.Failure("An error occurred while retrieving files", 500);
    }
}

/// <inheritdoc />
public async Task<Result<CmdResponse>> DeleteContentFileAsync(
    DeleteContentFileRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        var content = await _dataContext.Query<CommunityContent>()
            .Where(c => c.Id == request.ContentId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (content == null)
            return Result<CmdResponse>.NotFound($"Content with Id {request.ContentId} does not exist");

        if (content.SocialMediaIdentityId != request.RequestingIdentityId)
            return Result<CmdResponse>.Forbidden("You do not have permission to remove files from this content");

        var file = await _dataContext.Query<CommunityContentFile>()
            .Where(f => f.Id == request.FileId && f.ContentId == request.ContentId && !f.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (file == null)
            return Result<CmdResponse>.NotFound($"File with Id {request.FileId} does not exist");

        file.IsDeleted = true;
        file.DeletedAt = DateTime.UtcNow;
        _dataContext.Update(file);
        await _dataContext.SaveChangesAsync(cancellationToken);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "File removed successfully"
        });
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("DeleteContentFile", "CommunityContentFile", request.FileId, ex.Message, ex);
        return Result<CmdResponse>.Failure("An error occurred while removing file", 500);
    }
}
```

- [ ] **Step 4: Create endpoints and validators**

Create `Community.Api/Features/Content/Files/Create/Endpoint.cs`:

```csharp
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Files.Create;

public static class CreateContentFileEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/content/{contentId:guid}/files", Tags = ["Community Content"],
        Summary = "Attach file to content",
        Description = "Attaches a storage file to content. Validates requester is the content author.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateContentFileVsaRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.CreateContentFileAsync(request, ct);
    }
}
```

Create `Community.Api/Features/Content/Files/Create/CreateContentFileValidator.cs`:

```csharp
using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Files.Create;

public sealed class CreateContentFileValidator : AbstractValidator<CreateContentFileVsaRequest>
{
    public CreateContentFileValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");
        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage File ID is required");
        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");
    }
}
```

Create `Community.Api/Features/Content/Files/GetList/Endpoint.cs`:

```csharp
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Files.GetList;

public static class GetContentFilesEndpoint
{
    [BoltHandler]
    [MapGet("/api/community/content/{contentId:guid}/files", Tags = ["Community Content"],
        Summary = "List content files",
        Description = "Returns all file attachments for a given content item.")]
    public static async Task<Result<List<ContentFileResponse>>> Handle(
        GetContentFilesRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.GetContentFilesAsync(request, ct);
    }
}
```

Create `Community.Api/Features/Content/Files/GetList/GetContentFilesValidator.cs`:

```csharp
using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Files.GetList;

public sealed class GetContentFilesValidator : AbstractValidator<GetContentFilesRequest>
{
    public GetContentFilesValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");
    }
}
```

Create `Community.Api/Features/Content/Files/Delete/Endpoint.cs`:

```csharp
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Content.Files.Delete;

public static class DeleteContentFileEndpoint
{
    [BoltHandler]
    [MapDelete("/api/community/content/{contentId:guid}/files/{fileId:guid}", Tags = ["Community Content"],
        Summary = "Remove file from content",
        Description = "Soft-deletes a file attachment. Validates requester is the content author.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteContentFileRequest request,
        IContentService contentService,
        CancellationToken ct)
    {
        return await contentService.DeleteContentFileAsync(request, ct);
    }
}
```

Create `Community.Api/Features/Content/Files/Delete/DeleteContentFileValidator.cs`:

```csharp
using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Files.Delete;

public sealed class DeleteContentFileValidator : AbstractValidator<DeleteContentFileRequest>
{
    public DeleteContentFileValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");
    }
}
```

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/Modules/XFramework.Community/
git commit -m "feat: Add Content File endpoints (create, list, delete)"
```

---

### Task 10: Community — Identity File Update Endpoint (B10)

**Files:**
- Create: `src/Modules/XFramework.Community/Community.Domain.Shared/Contracts/Requests/UpdateIdentityFileRequest.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/ICommunityService.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/CommunityService.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/CommunityIdentities/Files/Update/Endpoint.cs`
- Create: `src/Modules/XFramework.Community/Community.Api/Features/CommunityIdentities/Files/Update/UpdateIdentityFileValidator.cs`

- [ ] **Step 1: Create UpdateIdentityFileRequest**

Create file `Community.Domain.Shared/Contracts/Requests/UpdateIdentityFileRequest.cs`:

```csharp
namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateIdentityFileRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<UpdateIdentityFileRequest, TResponse>
{
    public Guid IdentityId { get; set; }
    public Guid FileId { get; set; }
    public Guid StorageFileId { get; set; }
    public Guid RequestingIdentityId { get; set; }
}
```

- [ ] **Step 2: Add method to ICommunityService**

In `Community.Api/Services/ICommunityService.cs`, add:

```csharp
Task<Result<CmdResponse>> UpdateIdentityFileAsync(
    UpdateIdentityFileRequest request,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Implement in CommunityService**

Add to `CommunityService.cs`:

```csharp
/// <inheritdoc />
public async Task<Result<CmdResponse>> UpdateIdentityFileAsync(
    UpdateIdentityFileRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        if (request.RequestingIdentityId != request.IdentityId)
            return Result<CmdResponse>.Forbidden("You can only update your own identity files");

        var file = await _dataContext.Query<CommunityIdentityFile>()
            .Where(f => f.Id == request.FileId && f.IdentityId == request.IdentityId && !f.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (file == null)
            return Result<CmdResponse>.NotFound($"Identity file with Id {request.FileId} does not exist");

        file.StorageId = request.StorageFileId;
        file.ModifiedAt = DateTime.UtcNow;

        _dataContext.Update(file);
        await _dataContext.SaveChangesAsync(cancellationToken);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Identity file updated successfully"
        });
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("UpdateIdentityFile", "CommunityIdentityFile", request.FileId, ex.Message, ex);
        return Result<CmdResponse>.Failure("An error occurred while updating identity file", 500);
    }
}
```

- [ ] **Step 4: Create Endpoint**

Create file `Community.Api/Features/CommunityIdentities/Files/Update/Endpoint.cs`:

```csharp
using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.CommunityIdentities.Files.Update;

public static class UpdateIdentityFileEndpoint
{
    [BoltHandler]
    [MapPut("/api/community/identities/{identityId:guid}/files/{fileId:guid}", Tags = ["Community Identity"],
        Summary = "Update identity file",
        Description = "Updates the storage reference for a profile or cover photo. Validates requester owns the identity.")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateIdentityFileRequest request,
        ICommunityService communityService,
        CancellationToken ct)
    {
        return await communityService.UpdateIdentityFileAsync(request, ct);
    }
}
```

- [ ] **Step 5: Create Validator**

Create file `Community.Api/Features/CommunityIdentities/Files/Update/UpdateIdentityFileValidator.cs`:

```csharp
using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.CommunityIdentities.Files.Update;

public sealed class UpdateIdentityFileValidator : AbstractValidator<UpdateIdentityFileRequest>
{
    public UpdateIdentityFileValidator()
    {
        RuleFor(x => x.IdentityId)
            .NotEmpty().WithMessage("Identity ID is required");
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage File ID is required");
        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");
    }
}
```

- [ ] **Step 6: Verify build**

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/Modules/XFramework.Community/
git commit -m "feat: Add Identity File Update endpoint (PUT /api/community/identities/{id}/files/{fileId})"
```

---

### Task 11: Community — Block Enforcement (B11)

**Files:**
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/IConnectionService.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/ConnectionService.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/ContentService.cs`
- Modify: `src/Modules/XFramework.Community/Community.Api/Services/FeedService.cs`

- [ ] **Step 1: Add IsBlockedAsync to IConnectionService**

In `Community.Api/Services/IConnectionService.cs`, add:

```csharp
/// <summary>
/// Checks if a block connection exists between two identities in either direction.
/// </summary>
Task<bool> IsBlockedAsync(Guid identityA, Guid identityB, CancellationToken cancellationToken = default);

/// <summary>
/// Gets the set of identity IDs that have an active block relationship (either direction) with the given identity.
/// </summary>
Task<HashSet<Guid>> GetBlockedIdentityIdsAsync(Guid identityId, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implement in ConnectionService**

In `ConnectionService.cs`, add before the closing brace:

```csharp
/// <inheritdoc />
public async Task<bool> IsBlockedAsync(Guid identityA, Guid identityB, CancellationToken cancellationToken = default)
{
    return await _dataContext.Query<CommunityConnection>()
        .Where(c => c.TypeId == Community.Domain.Shared.CommunityConnectionTypes.Block)
        .Where(c => !c.IsDeleted && c.IsEnabled)
        .Where(c =>
            (c.SourceSocialMediaIdentityId == identityA && c.TargetSocialMediaIdentityId == identityB) ||
            (c.SourceSocialMediaIdentityId == identityB && c.TargetSocialMediaIdentityId == identityA))
        .AnyAsync(cancellationToken);
}

/// <inheritdoc />
public async Task<HashSet<Guid>> GetBlockedIdentityIdsAsync(Guid identityId, CancellationToken cancellationToken = default)
{
    var blocked = await _dataContext.Query<CommunityConnection>()
        .Where(c => c.TypeId == Community.Domain.Shared.CommunityConnectionTypes.Block)
        .Where(c => !c.IsDeleted && c.IsEnabled)
        .Where(c => c.SourceSocialMediaIdentityId == identityId || c.TargetSocialMediaIdentityId == identityId)
        .Select(c => c.SourceSocialMediaIdentityId == identityId
            ? c.TargetSocialMediaIdentityId
            : c.SourceSocialMediaIdentityId)
        .ToListAsync(cancellationToken);

    return blocked.ToHashSet();
}
```

- [ ] **Step 3: Add block check to ConnectionService.CreateConnectionAsync**

In `ConnectionService.cs`, in `CreateConnectionAsync`, add after the self-connection check (after line 43):

```csharp
// Check if either party has blocked the other
if (await IsBlockedAsync(request.SourceIdentityId, request.TargetIdentityId, cancellationToken))
    return Result<CmdResponse>.Forbidden("Cannot create connection — a block exists between these identities");
```

- [ ] **Step 4: Add IConnectionService to ContentService**

In `ContentService.cs`, add a constructor parameter:

```csharp
private readonly IConnectionService _connectionService;

public ContentService(
    IDataContext dataContext,
    IConnectionService connectionService,
    ILogger<ContentService> logger)
{
    _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
    _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

- [ ] **Step 5: Add block checks to ContentService methods**

In `GetContentAsync`, after finding the content (after line 128), add:

```csharp
// Block check: if the request has a RequesterIdentityId, check blocks
// GetContentRequest doesn't have a requester field — this is a public read.
// For full block enforcement, we'd need to add RequesterIdentityId to the request.
// For now, this endpoint remains public. Block enforcement applies to feed/search/reactions.
```

In `CreateContentReactionAsync`, after validating content and identity (after line 243), add:

```csharp
// Block check
if (await _connectionService.IsBlockedAsync(request.IdentityId, content.SocialMediaIdentityId, cancellationToken))
    return Result<CmdResponse>.Forbidden("Cannot react — a block exists between you and the content author");
```

In `GetCommunityIdentityAsync`, the `GetCommunityIdentityRequest` only has `Id` (no requester). To enforce block checks, we need a requester context. Add a note that this requires a request contract update in a future task, or skip for now since it's a profile view.

In `SearchIdentitiesAsync`, after the base query is built (after line 416), add:

```csharp
// Block enforcement: exclude blocked identities if a requester context is provided
if (request.RequestingIdentityId.HasValue && request.RequestingIdentityId.Value != Guid.Empty)
{
    var blockedIds = await _connectionService.GetBlockedIdentityIdsAsync(request.RequestingIdentityId.Value, cancellationToken);
    if (blockedIds.Count > 0)
    {
        query = query.Where(i => !blockedIds.Contains(i.Id));
    }
}
```

Note: `SearchIdentitiesRequest` needs a `RequestingIdentityId` property added. In `Community.Domain.Shared/Contracts/Requests/SearchIdentitiesRequest.cs`, add:

```csharp
public Guid? RequestingIdentityId { get; set; }
```

- [ ] **Step 6: Add IConnectionService to FeedService and enforce blocks**

In `FeedService.cs`, add constructor parameter:

```csharp
private readonly IConnectionService _connectionService;

public FeedService(
    IDataContext dataContext,
    IConnectionService connectionService,
    ILogger<FeedService> logger)
{
    _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
    _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

In `GetFeedAsync`, after building `feedSourceIds` (after line 57), add:

```csharp
// Remove blocked identities from feed sources
var blockedIds = await _connectionService.GetBlockedIdentityIdsAsync(request.IdentityId, cancellationToken);
if (blockedIds.Count > 0)
{
    feedSourceIds.RemoveAll(id => blockedIds.Contains(id));
}
```

- [ ] **Step 7: Verify build**

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/Modules/XFramework.Community/
git commit -m "feat: Full block enforcement — filter feed, search, reactions, connections"
```

---

### Task 12: Final Build Verification

- [ ] **Step 1: Build both services**

Run: `dotnet build src/Modules/XFramework.Communications/Communications.Api/Communications.Api.csproj`
Expected: Build succeeded

Run: `dotnet build src/Modules/XFramework.Community/Community.Api/Community.Api.csproj`
Expected: Build succeeded

- [ ] **Step 2: Full solution build**

Run: `dotnet build XFramework.sln`
Expected: Build succeeded (or warnings only, no errors)

- [ ] **Step 3: Commit any remaining fixes**

If build fixes were needed, commit them.
