namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public Guid? Guid { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Logo { get; set; }
    public int Status { get; set; }


    public LaboratoryEntityResponse? Entity { get; set; }
    public List<LaboratoryLocationResponse> LaboratoryLocations { get; set; }
    public List<LaboratoryMemberResponse> LaboratoryMembers { get; set; }
}