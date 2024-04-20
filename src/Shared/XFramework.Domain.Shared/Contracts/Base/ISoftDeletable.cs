namespace XFramework.Domain.Shared.Contracts.Base;

public interface ISoftDeletable
{
    public bool IsEnabled { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

}