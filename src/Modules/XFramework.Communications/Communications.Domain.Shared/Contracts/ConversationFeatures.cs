namespace Communications.Domain.Shared.Contracts;

[Flags]
public enum ConversationFeatures
{
    None = 0,
    ReadReceipts = 1,
    Typing = 2,
    Threads = 4,
    Reactions = 8,
    Replies = 16,
    Voice = 32,
    Attachments = 64,
    All = 127
}
