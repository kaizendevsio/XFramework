namespace Attendance.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AttendanceContextResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public AttendanceContextType ContextType { get; set; }
    public string? Description { get; set; }
    public Guid? DefaultPolicyId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

