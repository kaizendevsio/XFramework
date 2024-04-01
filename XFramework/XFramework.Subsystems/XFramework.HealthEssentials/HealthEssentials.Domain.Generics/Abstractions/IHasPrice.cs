namespace HealthEssentials.Domain.Generics.Abstractions;

public interface IHasPrice
{
    public decimal PriceEx { get; set; }
    
    public decimal PriceInc { get; set; }
    
    public decimal PriceTax => PriceInc - PriceEx;
}