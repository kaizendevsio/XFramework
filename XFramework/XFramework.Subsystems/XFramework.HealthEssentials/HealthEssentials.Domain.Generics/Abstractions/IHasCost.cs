namespace HealthEssentials.Domain.Generics.Abstractions;

public interface IHasCost
{
    public decimal CostEx { get; set; }
    
    public decimal CostInc { get; set; }
    
    public decimal CostTax => CostInc - CostEx;
}