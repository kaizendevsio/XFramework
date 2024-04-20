namespace PaymentGateways.Domain.BusinessObjects.Paynamics;

public class PaynamicsConfiguration
{
    public string MerchantId { get; set; }
    public string MerchantKey{ get; set; }
    public string CancelUrl { get; set; }
    public string NotificationUrl { get; set; }
    public string ResponseUrl { get; set; }
}