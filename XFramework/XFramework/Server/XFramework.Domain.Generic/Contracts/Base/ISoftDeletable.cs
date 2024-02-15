namespace XFramework.Domain.Generic.Contracts.Base;

public interface ISoftDeletable
{
    public bool IsEnabled { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

}