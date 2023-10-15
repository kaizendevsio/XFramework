namespace XFramework.Domain.Generic.Contracts.Base;

public interface IHasConcurrencyStamp
{
    public Guid ConcurrencyStamp { get; set; }
}