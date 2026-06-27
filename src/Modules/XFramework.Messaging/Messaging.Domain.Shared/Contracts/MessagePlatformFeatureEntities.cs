namespace Messaging.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThreadInvite : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(1)]
    public Guid InvitedCredentialId { get; set; }

    [MemoryPackOrder(2)]
    public Guid InvitedByCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public short Status { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? RespondedAt { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessagePin : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(1)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(2)]
    public Guid PinnedByMemberId { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageSaved : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid MessageThreadMemberId { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageHidden : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid MessageThreadMemberId { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageReport : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid ReporterMemberId { get; set; }

    [MemoryPackOrder(2)]
    public string Reason { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string? Details { get; set; }

    [MemoryPackOrder(4)]
    public short Status { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageReportAudit : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid ReportId { get; set; }

    [MemoryPackOrder(1)]
    public string Action { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public Guid? ActorCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? AssignedCredentialId { get; set; }

    [MemoryPackOrder(4)]
    public short? FromStatus { get; set; }

    [MemoryPackOrder(5)]
    public short? ToStatus { get; set; }

    [MemoryPackOrder(6)]
    public string? Note { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageModerationRule : BaseModel
{
    [MemoryPackOrder(0)]
    public string Name { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public string MatchType { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string Pattern { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string Action { get; set; } = string.Empty;

    [MemoryPackOrder(4)]
    public string? Description { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageBlock : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid BlockerCredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid BlockedCredentialId { get; set; }
}
