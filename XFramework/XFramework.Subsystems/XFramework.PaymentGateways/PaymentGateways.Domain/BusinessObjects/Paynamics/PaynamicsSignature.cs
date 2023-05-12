using PaymentGateways.Domain.Contracts.Requests.Paynamics;

namespace PaymentGateways.Domain.BusinessObjects.Paynamics;

public class PaynamicsSignature: PaynamicsTransactionRequest
{
    public string Merchant_Key { get; set; }
    public override string ToString() => $"{Mid}{Request_id}{Ip_address}{Notification_url}{Response_url}{Fname}{Lname}{Mname}{Address1}{Address2}{City}{State}{Country}{Zip}{Email}{Phone}{Client_ip}{Amount}{Currency}{Secure3d}{Merchant_Key}";
}