namespace Attendance.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record RemoveAttendanceParticipantRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<RemoveAttendanceParticipantRequest, CmdResponse>
{
    public Guid? TenantId { get; set; }
    public Guid ParticipantId { get; set; }
    public DateTime? EndedAt { get; set; }
}

