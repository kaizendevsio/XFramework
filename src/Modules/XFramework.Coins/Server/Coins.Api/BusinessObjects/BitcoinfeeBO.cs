namespace Coins.Api.BusinessObjects;

/// <summary>
/// Bitcoin fee recommendations
/// </summary>
public class BitcoinfeeBO
{
    public decimal FastestFee { get; set; }
    public decimal HalfHourFee { get; set; }
    public decimal HourFee { get; set; }
}