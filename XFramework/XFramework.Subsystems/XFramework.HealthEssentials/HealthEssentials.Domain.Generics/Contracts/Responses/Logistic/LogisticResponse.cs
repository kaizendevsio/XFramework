namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public string Guid { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Logo { get; set; }

    public LogisticEntityResponse? Entity { get; set; }
}