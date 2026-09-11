using System.Text.Json.Serialization;

namespace Yap.Contracts;

// Browser-facing models contain chat data only, never service credentials or actor tokens.
public sealed record UserSession(Guid CredentialId, Guid TenantId, string Name);
public sealed record SessionResponse(UserSession? User, string AntiforgeryToken);
public sealed record Person(Guid Id, string Name, string UserName);
public sealed record ReactionType(Guid Id, string Name, string Emoji);
public sealed record ChatDefaults(Guid ThreadTypeId, List<ReactionType> Reactions);
public sealed record ChatPage<T>(List<T> Items, int TotalCount);
public sealed record MessageQuote(Guid Id, string Sender, string Text);
public sealed record ChatAttachment(Guid Id, string Name, string ContentType, long Size);

public sealed class Conversation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Conversation";
    public bool Group { get; set; }
    public int Members { get; set; }
    public int Unread { get; set; }
    public bool Muted { get; set; }
    public string Preview { get; set; } = "Start a conversation";
    public DateTime? LastMessageAt { get; set; }
    public List<Person> People { get; set; } = [];
    [JsonIgnore] public List<ChatMessage> Messages { get; set; } = [];
    public int MessageTotal { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public bool Mine { get; set; }
    public Guid? ParentId { get; set; }
    public MessageQuote? Quote { get; set; }
    public bool Pinned { get; set; }
    public bool Saved { get; set; }
    [JsonIgnore] public List<ChatMessage> Replies { get; set; } = [];
    public int ReplyTotal { get; set; }
    public Dictionary<string, int> Reactions { get; set; } = [];
    public Dictionary<string, Guid> MyReactionIds { get; set; } = [];
    public List<ChatAttachment> Attachments { get; set; } = [];
    public string Delivery { get; set; } = "Sent";
    public string Initials => InitialsFor(Sender);
    public string Color => Mine ? "g1" : "g3";
    public string Time => CreatedAt.ToLocalTime().ToString("HH:mm");
    public static string InitialsFor(string? text) => string.Concat((text ?? "?")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => char.ToUpperInvariant(s[0])));
}

public sealed record SendMessage(Guid Id, Guid ThreadId, string Text, Guid? ParentId = null);
public sealed record MessageReceipt(Guid MessageId);
public sealed record CreateConversation(string Name, List<Guid> Members, bool Group);
public sealed record MessageAction(Guid ThreadId, Guid MessageId, string Action, string? Text = null,
    Guid? ReactionTypeId = null, Guid? ReactionId = null);
public sealed record ReadMessages(Guid ThreadId, List<Guid> MessageIds);
public sealed record ThreadAction(Guid ThreadId, string Action, bool Value);
public sealed record AttachMessageFile(Guid ThreadId, Guid MessageId, Guid StorageId);
public sealed record SearchHit(Guid ThreadId, Guid MessageId, string Text, DateTime CreatedAt);
