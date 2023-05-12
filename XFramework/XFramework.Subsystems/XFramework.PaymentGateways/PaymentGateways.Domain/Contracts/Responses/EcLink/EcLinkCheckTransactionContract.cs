using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Responses.EcLink;

public class EcLinkCheckTransactionContract
{
    [JsonPropertyName("?xml")]
    public EcLinkCheckTransactionContractXml EcLinkCheckTransactionContractXml { get; set; }

    [JsonPropertyName("soap:Envelope")]
    public EcLinkCheckTransactionContractSoapEnvelope EcLinkCheckTransactionContractSoapEnvelope { get; set; }
}
    
public class EcLinkCheckTransactionContractXml
{
    [JsonPropertyName("@version")]
    public string Version { get; set; }

    [JsonPropertyName("@encoding")]
    public string Encoding { get; set; }
}

public class EcLinkCheckTransactionContractConfirmPaymentResponse
{
    [JsonPropertyName("@xmlns")]
    public string Xmlns { get; set; }

    [JsonPropertyName("ConfirmPaymentResult")]
    public string ConfirmPaymentResultString { get; set; }
    [JsonIgnore]
    public List<EcLinkCheckTransactionContractConfirmPaymentResult> ConfirmPaymentResult => JsonSerializer.Deserialize<List<EcLinkCheckTransactionContractConfirmPaymentResult>>(ConfirmPaymentResultString) ?? null;
}

public class EcLinkCheckTransactionContractSoapBody
{
    [JsonPropertyName("ConfirmPaymentResponse")]
    public EcLinkCheckTransactionContractConfirmPaymentResponse EcLinkCheckTransactionContractConfirmPaymentResponse { get; set; }
}

public class EcLinkCheckTransactionContractSoapEnvelope
{
    [JsonPropertyName("@xmlns:soap")]
    public string XmlnsSoap { get; set; }

    [JsonPropertyName("@xmlns:xsi")]
    public string XmlnsXsi { get; set; }

    [JsonPropertyName("@xmlns:xsd")]
    public string XmlnsXsd { get; set; }

    [JsonPropertyName("soap:Body")]
    public EcLinkCheckTransactionContractSoapBody EcLinkCheckTransactionContractSoapBody { get; set; }
}

public class EcLinkCheckTransactionContractConfirmPaymentResult
{
    [JsonPropertyName("resultCode")]
    public string ResultCode { get; set; }

    [JsonPropertyName("result")]
    public string ResultString { get; set; }
    [JsonIgnore]
    public List<EcLinkCheckTransactionContractConfirmPaymentResultResult> Result => JsonSerializer.Deserialize<List<EcLinkCheckTransactionContractConfirmPaymentResultResult>>(ResultString) ?? null;
}
    
public class EcLinkCheckTransactionContractConfirmPaymentResultResult
{
    [JsonPropertyName("TransID")]
    public int TransID { get; set; }

    [JsonPropertyName("TransDate")]
    public string TransDate { get; set; }

    [JsonPropertyName("Service")]
    public string Service { get; set; }

    [JsonPropertyName("MerchantCode")]
    public string MerchantCode { get; set; }

    [JsonPropertyName("MerchantName")]
    public string MerchantName { get; set; }

    [JsonPropertyName("ReferenceNo")]
    public string ReferenceNo { get; set; }

    [JsonPropertyName("Amount")]
    public double Amount { get; set; }

    [JsonPropertyName("ExpiryDate")]
    public string ExpiryDate { get; set; }

    [JsonPropertyName("Remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("PaymentStatus")]
    public int PaymentStatus { get; set; }

    [JsonPropertyName("PaymentDate")]
    public object PaymentDate { get; set; }
}