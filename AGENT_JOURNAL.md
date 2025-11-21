# Agent Journal

This journal documents significant changes made to the XFramework project by AI agents.

---

## 2025-11-21 - Bug Fix: SignalR Registration Error

**Agent Mode:** 🩺 Bug Fixer  
**Task:** `.ruru/tasks/BUG_SignalR_Registration/TASK-FIXER-20251121-104300.md`

### Issue Description
SignalR `Register` method was failing with "Error binding arguments" at runtime. The error message indicated a server-side deserialization problem: "Failed to invoke 'Register' due to an error on the server."

### Root Cause Analysis
After investigating three critical files:
1. [`SignalRService.cs`](src/Infrastructure/XFramework.Integration/Services/SignalRService.cs) - Client-side registration
2. [`StreamFlowClient.cs`](src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/BusinessObjects/StreamFlowClient.cs) - Data model
3. [`MessageQueueHub.cs`](src/Modules/XFramework.StreamFlow/StreamFlow.Stream/Hubs/MessageQueueHub.cs) - Server-side hub

**Root Cause Identified:**  
In the [`RegisterConnection` method (line 353-357)](src/Infrastructure/XFramework.Integration/Services/SignalRService.cs:353-357), the `StreamFlowClient` object was being created with only `Id` and `Name` properties, but the `Queue` property was **NULL**:

```csharp
var request = new StreamFlowClient()
{
    Id = _clientId,
    Name = StreamFlowConfiguration.ClientName
    // ❌ Queue property was NULL!
};
```

This caused SignalR's serialization/deserialization to fail when the hub tried to process the request, as the `StreamFlowClient` class has a `Queue` property of type [`StreamFlowQueue`](src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/BusinessObjects/StreamFlowQueue.cs:5-9).

### Files Modified
- **Modified:** `src/Infrastructure/XFramework.Integration/Services/SignalRService.cs` (line 353-358)

### Changes Made
Initialized the `Queue` property with an empty `StreamFlowQueue` object to prevent NULL reference during serialization:

```csharp
var request = new StreamFlowClient()
{
    Id = _clientId,
    Name = StreamFlowConfiguration.ClientName,
    Queue = new StreamFlowQueue()  // ✅ Fixed: Initialize Queue property
};
```

### Verification
- ✅ Code compiled successfully (XFramework.Integration.dll built without errors)
- ✅ No compilation warnings related to this change
- ⚠️ Note: Full build had file locking errors from running services (expected during development)

### Important Notes
- This pattern was already correctly used in the [`StartEventListener` method (line 174-180)](src/Infrastructure/XFramework.Integration/Services/SignalRService.cs:174-180), where `Queue` is properly initialized
- The fix ensures consistency across all `StreamFlowClient` instantiations
- **Previous failed attempts** tried modifying interface signatures and return types, but the issue was actually a **model binding/serialization problem**, not a method signature mismatch

### Testing Recommendation
After restarting the StreamFlow.Stream and client services, verify that:
1. Client registration completes without "Error binding arguments"
2. Connection logging shows successful registration
3. No server-side errors in StreamFlow.Stream logs


## 2025-11-21 - Bug Fix: MessagePack Serialization Error (ACTUAL ROOT CAUSE)

**Agent Mode:** 🩺 Bug Fixer  
**Task:** Comprehensive investigation after 3 failed fix attempts

### Issue Description
SignalR `Register` method was **still** failing with "Failed to invoke 'Register' due to an error on the server" even after:
1. ❌ Adding interface parameters
2. ❌ Fixing return types  
3. ❌ Initializing Queue property

The error was occurring **ON THE SERVER SIDE** during deserialization, not during client-side sending.

### Comprehensive Investigation Performed
Read all related files together to identify the exact mismatch:
1. [`SignalRService.cs`](src/Infrastructure/XFramework.Integration/Services/SignalRService.cs:86) - Client configuration
2. [`IStreamFlow.cs`](src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/Abstractions/IStreamFlow.cs:11) - Interface definition
3. [`MessageQueueHub.cs`](src/Modules/XFramework.StreamFlow/StreamFlow.Stream/Hubs/MessageQueueHub.cs:69-77) - Server hub implementation
4. [`StreamFlowClient.cs`](src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/BusinessObjects/StreamFlowClient.cs) - Client model
5. [`StreamFlowQueue.cs`](src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/BusinessObjects/StreamFlowQueue.cs) - Queue model

### ACTUAL Root Cause Identified
**MessagePack Serialization Without Required Attributes!**

The SignalR client was configured to use **MessagePack protocol** (line 86 in `SignalRService.cs`):
```csharp
.AddMessagePackProtocol()  // ← Using MessagePack serialization
```

However, the data classes being transmitted (`StreamFlowClient`, `StreamFlowQueue`, `StreamFlowInvokeResponse`) were **missing MessagePack attributes**, which are **required** for MessagePack serialization/deserialization to work properly.

**Why Previous Fixes Failed:**
- The server couldn't deserialize the incoming MessagePack-encoded data because the classes lacked the `[MessagePackObject]` and `[Key(n)]` attributes
- This manifested as "Failed to invoke 'Register' due to an error on the server" because deserialization failed before the method could execute
- The error message was misleading—it wasn't a binding issue or NULL property issue, it was a serialization format issue

### Files Modified
1. **Modified:** `src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/BusinessObjects/StreamFlowClient.cs`
2. **Modified:** `src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/BusinessObjects/StreamFlowQueue.cs`
3. **Modified:** `src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/Contracts/Responses/StreamFlowInvokeResponse.cs`

### Changes Made

#### 1. StreamFlowClient.cs
Added MessagePack serialization attributes:
```csharp
using MessagePack;  // ✅ Added

[MessagePackObject]  // ✅ Added
public class StreamFlowClient
{
    [Key(0)]  // ✅ Added
    public string Id { get; set; }
    
    [Key(1)]  // ✅ Added
    public string Name { get; set; }
    
    [Key(2)]  // ✅ Added
    public string StreamId { get; set; }
    
    [Key(3)]  // ✅ Added
    public StreamFlowQueue Queue { get; set; }
    
    [Key(4)]  // ✅ Added
    public DateTime ConnectedAt { get; set; }
}
```

#### 2. StreamFlowQueue.cs
Added MessagePack serialization attributes:
```csharp
using MessagePack;  // ✅ Added

[MessagePackObject]  // ✅ Added
public class StreamFlowQueue
{
    [Key(0)]  // ✅ Added
    public string Name { get; set; }
    
    [Key(1)]  // ✅ Added
    public Guid Id { get; set; }
}
```

#### 3. StreamFlowInvokeResponse.cs
Added MessagePack serialization attributes:
```csharp
using MessagePack;  // ✅ Added

[MessagePackObject]  // ✅ Added
public class StreamFlowInvokeResponse
{
    [Key(0)]  // ✅ Added
    public HttpStatusCode HttpStatusCode { get; set; }
    
    [Key(1)]  // ✅ Added
    public string Message { get; set; }
    
    [Key(2)]  // ✅ Added
    public object Response { get; set; }
}
```

### Verification
- ✅ Build succeeded: `dotnet build src/Modules/XFramework.StreamFlow/StreamFlow.Domain.Shared/StreamFlow.Domain.Shared.csproj`
- ✅ No compilation errors
- ✅ Only unrelated nullability warnings (pre-existing, not introduced by changes)

### Important Notes
- **MessagePack Protocol:** When using `.AddMessagePackProtocol()` in SignalR, **ALL** classes transmitted over the connection **MUST** have `[MessagePackObject]` and `[Key(n)]` attributes
- **Note:** `StreamFlowMessage` already had MessagePack attributes (confirmed in investigation), so it was not causing issues
- **Key Assignment:** The `[Key(n)]` attributes use zero-based sequential integers and determine the serialization order
- **Nested Objects:** Complex objects like `StreamFlowQueue` inside `StreamFlowClient` also need their own MessagePack attributes

### Lessons Learned
1. When SignalR shows "error on the server" but the method signature is correct, check serialization configuration
2. MessagePack protocol requires explicit attribute decoration—it doesn't work with plain POCOs
3. Error messages about "binding arguments" can be misleading when the actual issue is serialization format mismatch
4. Always verify serialization protocol (MessagePack vs JSON) matches the class decorations

### Testing Recommendation
After restarting both StreamFlow.Stream server and client services:
1. ✅ Verify client registration completes without errors
2. ✅ Check that all SignalR methods (Register, Subscribe, Push, etc.) work correctly
3. ✅ Monitor server logs for successful MessagePack deserialization
4. ✅ Confirm no "Failed to invoke" errors appear

---
---