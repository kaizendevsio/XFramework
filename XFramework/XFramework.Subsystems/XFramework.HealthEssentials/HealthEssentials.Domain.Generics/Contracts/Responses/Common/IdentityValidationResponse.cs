namespace HealthEssentials.Domain.Generics.Contracts.Responses.Common;

public class IdentityValidationResponse
{
    public bool? IsExisting { get; set; }
    public bool? IsActivated { get; set; }
}