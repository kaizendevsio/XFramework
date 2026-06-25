namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceParticipantResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContextId { get; set; }
    public Guid CredentialId { get; set; }
    public string? DisplayName { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
}

