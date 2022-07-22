namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

public class CreateDoctorEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }
}