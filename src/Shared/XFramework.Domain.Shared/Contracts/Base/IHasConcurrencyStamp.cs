namespace XFramework.Domain.Shared.Contracts.Base;

public interface IHasConcurrencyStamp
{
    public Guid ConcurrencyStamp { get; set; }
}