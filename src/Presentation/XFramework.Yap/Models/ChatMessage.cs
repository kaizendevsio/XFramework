using Communications.Domain.Shared.Contracts.Responses;

namespace Yap.Models;

public sealed record ChatMessage(Guid Id, Guid SenderId, string Sender, string Text,
    DateTime CreatedAt, bool Mine, Guid? ParentId, bool Pinned, bool Saved)
{
    public IReadOnlyList<MessageReactionSummaryResponse> Reactions { get; init; } = [];
    public string Initials => InitialsFor(Sender);
    public string Time => CreatedAt.ToLocalTime().ToString("HH:mm");
    public static string InitialsFor(string? value) => string.Concat((value ?? "?")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => char.ToUpperInvariant(s[0])));
}
