using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Responses.EcLink;

public class EcLinkTransactionContract
{
    [JsonPropertyName("?xml")]
    public EcLinkTransactionResponseContractXml EcLinkTransactionResponseContractXml { get; set; }

    [JsonPropertyName("soap:Envelope")]
    public EcLinkTransactionResponseContractSoapEnvelope SoapEnvelope { get; set; }
        
    public List<CommitPaymentResultContract> TransactResult { get => JsonSerializer.Deserialize<List<CommitPaymentResultContract>>(SoapEnvelope.SoapBody.CommitPaymentResponse.CommitPaymentResult); }
}
    
public class EcLinkTransactionResponseContractXml
{
    [JsonPropertyName("@version")]
    public string Version { get; set; }

    [JsonPropertyName("@encoding")]
    public string Encoding { get; set; }
}

public class EcLinkTransactionResponseContractCommitPaymentResponse
{
    [JsonPropertyName("@xmlns")]
    public string Xmlns { get; set; }

    [JsonPropertyName("CommitPaymentResult")]
    public string CommitPaymentResult { get; set; }
}

public class EcLinkTransactionResponseContractSoapBody
{
    [JsonPropertyName("CommitPaymentResponse")]
    public EcLinkTransactionResponseContractCommitPaymentResponse CommitPaymentResponse { get; set; }
}

public class EcLinkTransactionResponseContractSoapEnvelope
{
    [JsonPropertyName("@xmlns:soap")]
    public string XmlnsSoap { get; set; }

    [JsonPropertyName("@xmlns:xsi")]
    public string XmlnsXsi { get; set; }

    [JsonPropertyName("@xmlns:xsd")]
    public string XmlnsXsd { get; set; }

    [JsonPropertyName("soap:Body")]
    public EcLinkTransactionResponseContractSoapBody SoapBody { get; set; }
}

public class CommitPaymentResultContract
{
    [JsonPropertyName("resultCode")]
    public string ResultCode { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; }
}