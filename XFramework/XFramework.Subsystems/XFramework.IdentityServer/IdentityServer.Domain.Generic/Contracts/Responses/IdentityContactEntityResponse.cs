namespace IdentityServer.Domain.Generic.Contracts.Responses;

public class IdentityContactEntityResponse
{
    public Guid? Guid { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }
}