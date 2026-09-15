using System.Text.Json.Serialization;

namespace Yap.Contracts;

// Browser-facing models contain chat data only, never service credentials or actor tokens.
public sealed record UserSession(Guid CredentialId, Guid TenantId, string Name, string? AvatarUrl = null);
public sealed record SessionResponse(UserSession? User, string AntiforgeryToken, bool EncryptionRequired = false);
public sealed record Person(Guid Id, string Name, string UserName, string? AvatarUrl = null, Guid MemberId = default, string Role = "Member", string? Nickname = null, DateTime? ActiveUntil = null, DateTime? LastActiveAt = null);
public sealed record ReactionType(Guid Id, string Name, string Emoji);
public sealed record ChatDefaults(Guid ThreadTypeId, List<ReactionType> Reactions);
public sealed record ChatPage<T>(List<T> Items, int TotalCount);
public sealed record MessageQuote(Guid Id, string Sender, string Text);
public sealed record ChatAttachment(Guid Id, string Name, string ContentType, long Size,
    Guid? EncryptionSenderDeviceId = null, long? SenderDirectoryRevision = null, EncryptedAttachmentKey? Key = null);

// This key travels only inside the signed, encrypted message payload.
public sealed record EncryptedAttachmentKey(string Algorithm, string Data);
public sealed record DeferredDeliveryPage(List<DeferredDelivery> Items, int TotalCount);
public sealed record DeferredDelivery(ChatMessage Message, string EnvelopeHash, List<Guid> PendingCredentialIds);

public sealed class Conversation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Conversation";
    public string? AvatarUrl { get; set; }
    public bool Group { get; set; }
    public int Members { get; set; }
    public int Unread { get; set; }
    public bool Muted { get; set; }
    public bool IsFavorite { get; set; }
    public bool Removed { get; set; }
    public string Preview { get; set; } = "Start a conversation";
    public DateTime? LastMessageAt { get; set; }
    public ChatMessage? LastMessage { get; set; }
    public List<Person> People { get; set; } = [];
    [JsonIgnore] public List<ChatMessage> Messages { get; set; } = [];
    public int MessageTotal { get; set; }
    public int Features { get; set; } = 127;
    public bool CanManage { get; set; }
    public bool ShareActiveStatus { get; set; } = true;
    public bool Allows(ChatFeature feature) => (Features & (int)feature) != 0;
    public string Initials => ChatMessage.InitialsFor(Name);
    public string Color => Group ? "g3" : "g1";
}

public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid SenderId { get; set; }
    public string Sender { get; set; } = "You";
    public string Text { get; set; } = "";
    public string? EncryptedEnvelope { get; set; }
    public long? AcceptedSenderDirectoryRevision { get; set; }
    public Guid? EncryptionSenderDeviceId { get; set; }
    public Dictionary<Guid, long> RecipientDirectoryRevisions { get; set; } = [];
    public bool EncryptionLocked { get; set; }
    public bool EncryptionPending { get; set; }
    public int PendingEncryptionCount { get; set; }
    public List<Guid> EncryptionAudienceCredentialIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public bool Mine { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsThreadReply { get; set; }
    public MessageQuote? Quote { get; set; }
    public bool Pinned { get; set; }
    public bool Saved { get; set; }
    [JsonIgnore] public List<ChatMessage> Replies { get; set; } = [];
    public int ReplyTotal { get; set; }
    public Dictionary<string, int> Reactions { get; set; } = [];
    public Dictionary<string, Guid> MyReactionIds { get; set; } = [];
    public List<ChatAttachment> Attachments { get; set; } = [];
    public bool HasAttachments { get; set; }
    public bool AttachmentLinksReady { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LocalFileKey { get; set; }
    public string Delivery { get; set; } = "Sent";
    public int ReadCount { get; set; }
    public List<Person> Readers { get; set; } = [];
    public List<Person> LatestReaders { get; set; } = [];
    public bool IsLatestOwnMessage { get; set; }
    public int DeliveredCount { get; set; }
    public string Initials => InitialsFor(Sender);
    public string Color => Mine ? "g1" : "g3";
    public string Time => CreatedAt.ToLocalTime().ToString("HH:mm");
    public static string InitialsFor(string? text) => string.Concat((text ?? "?")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => char.ToUpperInvariant(s[0])));
}

public sealed record SendMessage(Guid Id, Guid ThreadId, string Text, Guid? ParentId = null, bool IsThreadReply = false,
    string? EncryptedEnvelope = null, List<Guid>? RecipientCredentialIds = null, Guid? EncryptionSenderDeviceId = null, long? SenderDirectoryRevision = null,
    Dictionary<Guid, long>? RecipientDirectoryRevisions = null);
public sealed record MessageReceipt(Guid MessageId);
public sealed record ChatUpdateHint(Guid ThreadId, string Kind, Guid? ActorId, List<Guid> MessageIds);
public sealed record CreateConversation(string Name, List<Guid> Members, bool Group);
public sealed record MessageAction(Guid ThreadId, Guid MessageId, string Action, string? Text = null,
    Guid? ReactionTypeId = null, Guid? ReactionId = null, string? EncryptedEnvelope = null,
    Guid? EncryptionSenderDeviceId = null, long? SenderDirectoryRevision = null, Dictionary<Guid, long>? RecipientDirectoryRevisions = null);
public sealed record ReadMessages(Guid ThreadId, List<Guid> MessageIds);
public sealed record ThreadAction(Guid ThreadId, string Action, bool Value);
public sealed record AttachMessageFile(Guid ThreadId, Guid MessageId, Guid StorageId);
public sealed record BeginUpload(string FileName, string ContentType, long TotalBytes);
public sealed record UploadTicket(Guid UploadId, int ChunkSizeBytes, int TotalParts);
public sealed record StoredFile(Guid Id);

/// <summary>Attachment sizing shared by the browser and the host so the two cannot drift.</summary>
public static class ChatLimits
{
    /// <summary>Largest attachment Yap accepts.</summary>
    public const long MaxFileBytes = 4L * 1024 * 1024 * 1024;
    /// <summary>Largest attachment copied onto the device so it can be queued offline.</summary>
    public const long StagedFileBytes = 64L * 1024 * 1024;
    /// <summary>Part size requested from storage; 4 GB lands in 512 parts.</summary>
    public const int PreferredChunkBytes = 8 * 1024 * 1024;
    /// <summary>Ceiling for a single part request, with slack for headers.</summary>
    public const long PartRequestBytes = PreferredChunkBytes + 65536;
}
public sealed record SearchHit(Guid ThreadId, Guid MessageId, string Text, DateTime CreatedAt);
public sealed record TypingUpdate(Guid ThreadId, Guid CredentialId, bool IsTyping);
public enum ChatFeature { ReadReceipts = 1, Typing = 2, Threads = 4, Reactions = 8, Replies = 16, Voice = 32, Attachments = 64 }
public sealed record ConversationUpdate(Guid ThreadId, int? Features = null, Guid? NicknameMemberId = null, string? Nickname = null, string? Name = null);
public sealed record ConversationMemberAction(Guid ThreadId, Guid CredentialId, Guid MemberId, string Action, string? Role = null);
